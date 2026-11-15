using System.Security.Claims;

using EntryHub.Api.Data;
using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Features.V1.Common;
using EntryHub.Api.Features.V1.Rooms.Blocks;
using EntryHub.Api.Features.V1.Rules;
using EntryHub.Api.Features.V1.Sites;
using EntryHub.Api.Security;

using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace EntryHub.Api.Features.V1.Bookings.RescheduleBooking;

public sealed class RescheduleBookingHandler(
    EntryHubDbContext db,
    BookingPolicy policy,
    TenantRulesReader rulesReader,
    TimeProvider clock,
    IAuthorizationService authorization)
{
    public async Task<RescheduleBookingResult> HandleAsync(
        ClaimsPrincipal user,
        Guid id,
        RescheduleBookingRequest request,
        CancellationToken cancellationToken)
    {
        var booking = await db.Bookings.SingleOrDefaultAsync(booking => booking.Id == id, cancellationToken);

        if (booking is null)
        {
            return new RescheduleBookingResult.NotFound();
        }

        if (!(await authorization.AuthorizeAsync(user, booking, BookingOperations.Reschedule)).Succeeded)
        {
            return new RescheduleBookingResult.Forbidden();
        }

        var now = clock.GetUtcNow();
        var rules = await rulesReader.GetAsync(cancellationToken);

        if (policy.EvaluateReschedule(rules, booking.Status, booking.StartAt, booking.EndAt, now, Caller.From(user).IsFacilities) is { } blocked)
        {
            return new RescheduleBookingResult.RulesBroken([blocked]);
        }

        var roomId = request.RoomId ?? booking.RoomId;
        var room = await db.Rooms.AsNoTracking().Include(room => room.Site)
            .SingleOrDefaultAsync(room => room.Id == roomId && room.IsActive && room.Site!.IsActive, cancellationToken);

        if (room is null)
        {
            return new RescheduleBookingResult.RulesBroken(
                [new(BookingRuleCodes.RoomUnavailable, "The room does not exist or is not available.")]);
        }

        // The same rules as a new booking. The limit of active bookings is not asked: moving one does not add one.
        var violations = policy.EvaluateNewBooking(rules, room.Site!.Hours, request.StartAt, request.EndAt, booking.Attendees, room.Capacity, now).ToList();

        if (await SiteClosures.CheckAsync(db, room.SiteId, room.Site.Hours, request.StartAt, cancellationToken) is { } closed)
        {
            violations.Add(closed);
        }

        if (await RoomBlocks.CheckAsync(db, room.Id, request.StartAt, request.EndAt, cancellationToken) is { } roomBlocked)
        {
            violations.Add(roomBlocked);
        }

        if (violations.Count > 0)
        {
            return new RescheduleBookingResult.RulesBroken(violations);
        }

        var newStart = request.StartAt.ToUniversalTime();

        // A booking moved to a later time must be reminded again at the new time.
        if (newStart > booking.StartAt)
        {
            booking.ReminderSentAt = null;
        }

        booking.RoomId = roomId;
        booking.BufferBeforeMinutes = room.BufferBeforeMinutes;
        booking.BufferAfterMinutes = room.BufferAfterMinutes;
        booking.StartAt = newStart;
        booking.EndAt = request.EndAt.ToUniversalTime();
        booking.RescheduledAt = now;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (LostToAnotherBooking(exception))
        {
            // The exclusion constraint of the database decides, as when a booking is created; it ignores the row
            // being moved, so a booking never collides with its own old time.
            return new RescheduleBookingResult.Overlaps();
        }

        return new RescheduleBookingResult.Rescheduled(BookingResponse.From(booking));
    }

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