using System.Security.Claims;

using EntryHub.Api.Data;
using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Features.V1.Common;
using EntryHub.Api.Security;

using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Features.V1.Bookings.CancelBooking;

public abstract record CancelBookingResult
{
    private CancelBookingResult()
    {
    }

    /// <summary>
    /// The booking is cancelled now, or it already was: cancelling twice is not an error.
    /// </summary>
    public sealed record Cancelled : CancelBookingResult;

    public sealed record NotFound : CancelBookingResult;

    public sealed record Forbidden : CancelBookingResult;

    public sealed record TooLate(RuleViolation Violation) : CancelBookingResult;
}

public sealed class CancelBookingHandler(
    EntryHubDbContext db,
    BookingPolicy policy,
    TimeProvider clock,
    IAuthorizationService authorization)
{
    public async Task<CancelBookingResult> HandleAsync(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken)
    {
        var booking = await db.Bookings.SingleOrDefaultAsync(booking => booking.Id == id, cancellationToken);

        if (booking is null)
        {
            return new CancelBookingResult.NotFound();
        }

        if (!(await authorization.AuthorizeAsync(user, booking, BookingOperations.Cancel)).Succeeded)
        {
            return new CancelBookingResult.Forbidden();
        }

        if (booking.Status == BookingStatus.Cancelled)
        {
            return new CancelBookingResult.Cancelled();
        }

        var now = clock.GetUtcNow();
        var caller = Caller.From(user);

        if (policy.EvaluateCancellation(booking.StartAt, now, caller.IsFacilities) is { } violation)
        {
            return new CancelBookingResult.TooLate(violation);
        }

        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAt = now;
        await db.SaveChangesAsync(cancellationToken);

        return new CancelBookingResult.Cancelled();
    }
}