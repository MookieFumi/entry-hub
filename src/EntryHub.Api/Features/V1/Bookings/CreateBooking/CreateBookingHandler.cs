using System.Security.Cryptography;
using System.Text;

using EntryHub.Api.Data;
using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Features.V1.Common;
using EntryHub.Api.Features.V1.Rules;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace EntryHub.Api.Features.V1.Bookings.CreateBooking;

public sealed class CreateBookingHandler(EntryHubDbContext db, BookingPolicy policy, TenantRulesReader rulesReader, TimeProvider clock)
{
    public Task<CreateBookingResult> HandleAsync(Caller caller, CreateBookingRequest request, CancellationToken cancellationToken) =>
        HandleAsync(caller, request, idempotencyKey: null, cancellationToken);

    public async Task<CreateBookingResult> HandleAsync(
        Caller caller,
        CreateBookingRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        string? requestHash = null;

        if (idempotencyKey is not null)
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > IdempotencyRecord.MaxKeyLength)
            {
                return new CreateBookingResult.KeyInvalid();
            }

            requestHash = HashOf(request);
            var earlier = await ReplayAsync(caller, idempotencyKey, requestHash, cancellationToken);

            if (earlier is not null)
            {
                return earlier;
            }
        }

        var room = await db.Rooms.AsNoTracking().Include(room => room.Site)
            .SingleOrDefaultAsync(room => room.Id == request.RoomId && room.IsActive && room.Site!.IsActive, cancellationToken);

        if (room is null)
        {
            return new CreateBookingResult.RulesBroken(
                [new(BookingRuleCodes.RoomUnavailable, "The room does not exist or is not available.")]);
        }

        var now = clock.GetUtcNow();
        var rules = await rulesReader.GetAsync(cancellationToken);
        var violations = policy.EvaluateNewBooking(rules, room.Site!.Hours, request.StartAt, request.EndAt, request.Attendees, room.Capacity, now).ToList();

        if (!caller.IsFacilities)
        {
            var active = await db.Bookings.CountAsync(
                booking => booking.OrganizerId == caller.Id
                    && booking.Status == BookingStatus.Confirmed
                    && booking.EndAt > now,
                cancellationToken);

            if (active >= rules.MaxActiveBookingsPerEmployee)
            {
                violations.Add(new(
                    BookingRuleCodes.TooManyActiveBookings,
                    $"You already have {active} active bookings; the limit is {rules.MaxActiveBookingsPerEmployee}."));
            }
        }

        if (violations.Count > 0)
        {
            return new CreateBookingResult.RulesBroken(violations);
        }

        var booking = new Booking
        {
            Id = Guid.CreateVersion7(),
            RoomId = room.Id,
            OrganizerId = caller.Id,
            Title = request.Title.Trim(),
            StartAt = request.StartAt.ToUniversalTime(),
            EndAt = request.EndAt.ToUniversalTime(),
            Attendees = request.Attendees,
            CreatedAt = now,
        };

        db.Bookings.Add(booking);

        if (idempotencyKey is not null)
        {
            db.IdempotencyRecords.Add(new IdempotencyRecord
            {
                Id = Guid.CreateVersion7(),
                UserId = caller.Id,
                Key = idempotencyKey,
                RequestHash = requestHash!,
                BookingId = booking.Id,
                CreatedAt = now,
            });
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (idempotencyKey is not null && (IsDuplicateKey(exception) || LostToAnotherBooking(exception)))
        {
            // Two requests with the same key raced: one of them wrote the key first. Forget what this one tried
            // to write and answer with what the winner created (or, if the key is not there, with the conflict).
            db.ChangeTracker.Clear();
            return await ReplayAsync(caller, idempotencyKey, requestHash!, cancellationToken)
                ?? new CreateBookingResult.Overlaps();
        }
        catch (Exception exception) when (LostToAnotherBooking(exception))
        {
            // No overlap check in C#: the exclusion constraint of the database is the only judge, so two
            // simultaneous requests cannot both win.
            return new CreateBookingResult.Overlaps();
        }

        return new CreateBookingResult.Created(BookingResponse.From(booking));
    }

    /// <summary>
    /// Answers a request whose key was already used: the original booking for the same request, a conflict for a
    /// different one, and <see langword="null"/> when the key is new (or has expired, in which case it is forgotten).
    /// </summary>
    private async Task<CreateBookingResult?> ReplayAsync(Caller caller, string key, string requestHash, CancellationToken cancellationToken)
    {
        var record = await db.IdempotencyRecords.AsNoTracking()
            .SingleOrDefaultAsync(record => record.UserId == caller.Id && record.Key == key, cancellationToken);

        if (record is null)
        {
            return null;
        }

        if (record.CreatedAt <= clock.GetUtcNow() - IdempotencyRecord.Lifetime)
        {
            await db.IdempotencyRecords.Where(expired => expired.Id == record.Id).ExecuteDeleteAsync(cancellationToken);
            return null;
        }

        if (record.RequestHash != requestHash)
        {
            return new CreateBookingResult.KeyReused();
        }

        var booking = await db.Bookings.AsNoTracking().SingleAsync(booking => booking.Id == record.BookingId, cancellationToken);
        return new CreateBookingResult.Created(BookingResponse.From(booking), Replayed: true);
    }

    private static string HashOf(CreateBookingRequest request)
    {
        var canonical = string.Join(
            '|',
            request.RoomId,
            request.Title.Trim(),
            request.StartAt.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            request.EndAt.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            request.Attendees);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static bool IsDuplicateKey(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: not null } postgres
                && postgres.ConstraintName.Contains("idempotency_records", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The exclusion violation is the usual loser. With several inserts racing for the same slot PostgreSQL can
    /// also pick one of them as the victim of a deadlock between their index checks. Both mean that another
    /// booking took the slot, and EF wraps the second one in an <see cref="InvalidOperationException"/>
    /// because Npgsql flags it as transient.
    /// </summary>
    private static bool LostToAnotherBooking(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation or PostgresErrorCodes.DeadlockDetected })
            {
                return true;
            }
        }

        return false;
    }
}