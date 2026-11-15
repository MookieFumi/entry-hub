using System.Security.Claims;

using EntryHub.Api.Data;
using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Security;

using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Features.V1.Bookings.CheckInBooking;

public abstract record CheckInBookingResult
{
    private CheckInBookingResult()
    {
    }

    /// <summary>
    /// The booking is checked in now, or it already was: the first moment is kept.
    /// </summary>
    public sealed record CheckedIn(BookingResponse Booking) : CheckInBookingResult;

    public sealed record NotFound : CheckInBookingResult;

    public sealed record Forbidden : CheckInBookingResult;

    public sealed record Refused(RuleViolation Violation) : CheckInBookingResult;
}

public sealed class CheckInBookingHandler(EntryHubDbContext db, BookingPolicy policy, TimeProvider clock, IAuthorizationService authorization)
{
    public async Task<CheckInBookingResult> HandleAsync(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken)
    {
        var booking = await db.Bookings.SingleOrDefaultAsync(booking => booking.Id == id, cancellationToken);

        if (booking is null)
        {
            return new CheckInBookingResult.NotFound();
        }

        if (!(await authorization.AuthorizeAsync(user, booking, BookingOperations.CheckIn)).Succeeded)
        {
            return new CheckInBookingResult.Forbidden();
        }

        if (booking.CheckedInAt is not null)
        {
            return new CheckInBookingResult.CheckedIn(BookingResponse.From(booking));
        }

        // The database keeps microseconds, so the moment is cut to them and reads back the same.
        var now = clock.GetUtcNow();
        now = new DateTimeOffset(now.Ticks - (now.Ticks % 10), now.Offset);

        if (policy.EvaluateCheckIn(booking.Status, booking.StartAt, booking.EndAt, now) is { } violation)
        {
            return new CheckInBookingResult.Refused(violation);
        }

        booking.CheckedInAt = now;
        await db.SaveChangesAsync(cancellationToken);

        return new CheckInBookingResult.CheckedIn(BookingResponse.From(booking));
    }
}