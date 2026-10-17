using EntryHub.Api.Data;
using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Features.V1.Common;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace EntryHub.Api.Features.V1.Bookings.CreateBooking;

public sealed class CreateBookingHandler(EntryHubDbContext db, BookingPolicy policy, TimeProvider clock)
{
    public async Task<CreateBookingResult> HandleAsync(Caller caller, CreateBookingRequest request, CancellationToken cancellationToken)
    {
        var room = await db.Rooms.AsNoTracking()
            .SingleOrDefaultAsync(room => room.Id == request.RoomId && room.IsActive, cancellationToken);

        if (room is null)
        {
            return new CreateBookingResult.RulesBroken(
                [new(BookingRuleCodes.RoomUnavailable, "The room does not exist or is not available.")]);
        }

        var now = clock.GetUtcNow();
        var violations = policy.EvaluateNewBooking(request.StartAt, request.EndAt, request.Attendees, room.Capacity, now).ToList();

        if (!caller.IsFacilities)
        {
            var active = await db.Bookings.CountAsync(
                booking => booking.OrganizerId == caller.Id
                    && booking.Status == BookingStatus.Confirmed
                    && booking.EndAt > now,
                cancellationToken);

            if (active >= policy.MaxActiveBookingsPerEmployee)
            {
                violations.Add(new(
                    BookingRuleCodes.TooManyActiveBookings,
                    $"You already have {active} active bookings; the limit is {policy.MaxActiveBookingsPerEmployee}."));
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

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation })
        {
            // No overlap check in C#: the exclusion constraint of the database is the only judge, so two
            // simultaneous requests cannot both win.
            return new CreateBookingResult.Overlaps();
        }

        return new CreateBookingResult.Created(BookingResponse.From(booking));
    }
}