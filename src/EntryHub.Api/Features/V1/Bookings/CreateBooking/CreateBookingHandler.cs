using System.Security.Cryptography;
using System.Text;

using EntryHub.Api.Data;
using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Domain.Rooms;
using EntryHub.Api.Features.V1.Common;
using EntryHub.Api.Features.V1.Rooms.Blocks;
using EntryHub.Api.Features.V1.Rules;
using EntryHub.Api.Features.V1.Sites;

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
        var onBehalfOf = string.IsNullOrWhiteSpace(request.OnBehalfOf) ? null : request.OnBehalfOf.Trim();

        if (onBehalfOf is not null && !caller.IsFacilities)
        {
            return new CreateBookingResult.Forbidden();
        }

        // Who the booking belongs to: it counts for the organizer's rules, reminders and permissions.
        var organizer = onBehalfOf ?? caller.Id;
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

        var active = caller.IsFacilities
            ? 0
            : await db.Bookings.CountAsync(
                booking => booking.OrganizerId == caller.Id
                    && booking.Status != BookingStatus.Cancelled
                    && booking.EndAt > now,
                cancellationToken);

        var series = request.Recurrence is { } recurrence ? BookingSeriesOccurrences.Plan(recurrence, request.StartAt, request.EndAt, room.Site!.Hours) : null;

        if (series is { Violation: { } invalid })
        {
            return new CreateBookingResult.RulesBroken([invalid]);
        }

        var plan = series?.Occurrences ?? [new PlannedOccurrence(1, request.StartAt, request.EndAt, null)];
        var bookings = new List<Booking>();
        var skipped = new List<SeriesOccurrence>();
        var occurrences = new List<SeriesOccurrence>();
        var seriesId = series is null ? (Guid?)null : Guid.CreateVersion7();

        foreach (var occurrence in plan)
        {
            var violations = occurrence.Violation is { } nonexistent ? [nonexistent] : new List<RuleViolation>();

            if (violations.Count == 0)
            {
                violations = await EvaluateAsync(caller, request, room, rules, occurrence, now, active + bookings.Count, cancellationToken);
            }

            if (violations.Count == 0 && series is not null && await IsTakenAsync(room, occurrence, cancellationToken))
            {
                violations.Add(new(BookingRuleCodes.RoomAlreadyBooked, "Another booking of this room overlaps this occurrence."));
            }

            if (series is null && violations.Count > 0)
            {
                return new CreateBookingResult.RulesBroken(violations);
            }

            if (violations.Count > 0)
            {
                occurrences.Add(new(occurrence.Position, occurrence.StartAt, occurrence.EndAt, null, violations));
                skipped.Add(occurrences[^1]);
                continue;
            }

            var booking = new Booking
            {
                Id = Guid.CreateVersion7(),
                RoomId = room.Id,
                OrganizerId = organizer,
                CreatedBy = organizer == caller.Id ? null : caller.Id,
                Title = request.Title.Trim(),
                StartAt = occurrence.StartAt.ToUniversalTime(),
                EndAt = occurrence.EndAt.ToUniversalTime(),
                Attendees = request.Attendees,
                Status = room.RequiresApproval && !caller.IsFacilities ? BookingStatus.Pending : BookingStatus.Confirmed,
                BufferBeforeMinutes = room.BufferBeforeMinutes,
                BufferAfterMinutes = room.BufferAfterMinutes,
                CreatedAt = now,
                SeriesId = seriesId,
                SeriesPosition = series is null ? null : occurrence.Position,
            };

            bookings.Add(booking);
            occurrences.Add(new(occurrence.Position, occurrence.StartAt, occurrence.EndAt, booking.Id, []));
        }

        if (bookings.Count == 0)
        {
            return new CreateBookingResult.RulesBroken([.. skipped.SelectMany(occurrence => occurrence.Violations).DistinctBy(violation => (violation.Code, violation.Message))]);
        }

        if (series is not null)
        {
            db.BookingSeries.Add(new BookingSeries
            {
                Id = seriesId!.Value,
                RoomId = room.Id,
                OrganizerId = organizer,
                Frequency = request.Recurrence!.Frequency,
                Interval = request.Recurrence.Interval,
                Count = request.Recurrence.Count,
                Until = request.Recurrence.Until,
                CreatedAt = now,
            });
        }

        db.Bookings.AddRange(bookings);
        var booking0 = bookings[0];

        if (idempotencyKey is not null)
        {
            db.IdempotencyRecords.Add(new IdempotencyRecord
            {
                Id = Guid.CreateVersion7(),
                UserId = caller.Id,
                Key = idempotencyKey,
                RequestHash = requestHash!,
                BookingId = booking0.Id,
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
            var replay = await ReplayAsync(caller, idempotencyKey, requestHash!, cancellationToken);

            // A deadlock victim can ask before the winner has committed, so its key is not there yet: the winner is
            // about to commit, which makes waiting for it better than answering with a conflict.
            for (var attempt = 0; replay is null && IsDeadlock(exception) && attempt < DeadlockReplayAttempts; attempt++)
            {
                await Task.Delay(DeadlockReplayDelay, cancellationToken);
                replay = await ReplayAsync(caller, idempotencyKey, requestHash!, cancellationToken);
            }

            return replay ?? new CreateBookingResult.Overlaps();
        }
        catch (Exception exception) when (LostToAnotherBooking(exception))
        {
            // The exclusion constraint of the database is the only judge, so two simultaneous requests cannot both
            // win. A series is saved whole: if one occurrence loses the race, none of them is created.
            return new CreateBookingResult.Overlaps();
        }

        var response = BookingResponse.From(booking0);

        return new CreateBookingResult.Created(series is null
            ? response
            : response with { Series = new SeriesOutcome(seriesId!.Value, plan.Count, bookings.Count, occurrences) });
    }

    private async Task<List<RuleViolation>> EvaluateAsync(
        Caller caller,
        CreateBookingRequest request,
        Room room,
        BookingRules rules,
        PlannedOccurrence occurrence,
        DateTimeOffset now,
        int active,
        CancellationToken cancellationToken)
    {
        var violations = policy.EvaluateNewBooking(rules, room.Site!.Hours, occurrence.StartAt, occurrence.EndAt, request.Attendees, room.Capacity, now).ToList();

        if (await SiteClosures.CheckAsync(db, room.SiteId, room.Site.Hours, occurrence.StartAt, cancellationToken) is { } closed)
        {
            violations.Add(closed);
        }

        if (await RoomBlocks.CheckAsync(db, room.Id, occurrence.StartAt, occurrence.EndAt, cancellationToken) is { } blocked)
        {
            violations.Add(blocked);
        }

        if (!caller.IsFacilities && active >= rules.MaxActiveBookingsPerEmployee)
        {
            violations.Add(new(
                BookingRuleCodes.TooManyActiveBookings,
                $"You already have {active} active bookings; the limit is {rules.MaxActiveBookingsPerEmployee}."));
        }

        return violations;
    }

    /// <summary>
    /// Only to tell a series which occurrences to skip: the exclusion constraint stays the judge of the ones it saves.
    /// </summary>
    private Task<bool> IsTakenAsync(Room room, PlannedOccurrence occurrence, CancellationToken cancellationToken)
    {
        var from = occurrence.StartAt.ToUniversalTime().AddMinutes(-room.BufferBeforeMinutes);
        var until = occurrence.EndAt.ToUniversalTime().AddMinutes(room.BufferAfterMinutes);

        return db.Bookings.AnyAsync(
            booking => booking.RoomId == room.Id
                && booking.Status != BookingStatus.Cancelled
                && booking.BlockedFrom < until
                && booking.BlockedUntil > from,
            cancellationToken);
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
        var response = BookingResponse.From(booking);

        if (booking.SeriesId is { } seriesId)
        {
            // The skipped occurrences were not saved, so a replay lists only the ones that were created.
            var created = await db.Bookings.AsNoTracking().Where(other => other.SeriesId == seriesId).OrderBy(other => other.SeriesPosition).ToListAsync(cancellationToken);
            var occurrences = created.Select(other => new SeriesOccurrence(other.SeriesPosition ?? 1, other.StartAt, other.EndAt, other.Id, [])).ToList();
            response = response with { Series = new SeriesOutcome(seriesId, occurrences.Count, occurrences.Count, occurrences) };
        }

        return new CreateBookingResult.Created(response, Replayed: true);
    }

    private static string HashOf(CreateBookingRequest request)
    {
        var canonical = string.Join(
            '|',
            request.RoomId,
            request.Title.Trim(),
            request.StartAt.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            request.EndAt.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            request.Attendees,
            request.OnBehalfOf?.Trim(),
            request.Recurrence is { } recurrence ? $"{recurrence.Frequency}/{recurrence.Interval}/{recurrence.Count}/{recurrence.Until:O}" : null);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private const int DeadlockReplayAttempts = 30;

    private static readonly TimeSpan DeadlockReplayDelay = TimeSpan.FromMilliseconds(100);

    private static bool IsDeadlock(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: PostgresErrorCodes.DeadlockDetected })
            {
                return true;
            }
        }

        return false;
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