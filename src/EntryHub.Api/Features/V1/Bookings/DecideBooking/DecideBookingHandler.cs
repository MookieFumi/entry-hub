using System.Security.Claims;

using EntryHub.Api.Data;
using EntryHub.Api.Domain.Bookings;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Features.V1.Bookings.DecideBooking;

public abstract record DecideBookingResult
{
    private DecideBookingResult()
    {
    }

    public sealed record Decided(BookingResponse Booking) : DecideBookingResult;

    public sealed record NotFound : DecideBookingResult;

    /// <summary>
    /// The booking is not waiting for a decision.
    /// </summary>
    public sealed record NotPending(BookingStatus Status) : DecideBookingResult;

    /// <summary>
    /// The booking has already ended, so approving it would confirm something in the past.
    /// </summary>
    public sealed record Expired : DecideBookingResult;
}

public sealed class DecideBookingHandler(EntryHubDbContext db, TimeProvider clock)
{
    public Task<DecideBookingResult> ApproveAsync(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken) =>
        DecideAsync(user, id, approve: true, reason: null, cancellationToken);

    public Task<DecideBookingResult> RejectAsync(ClaimsPrincipal user, Guid id, string? reason, CancellationToken cancellationToken) =>
        DecideAsync(user, id, approve: false, reason, cancellationToken);

    private async Task<DecideBookingResult> DecideAsync(ClaimsPrincipal user, Guid id, bool approve, string? reason, CancellationToken cancellationToken)
    {
        var booking = await db.Bookings.SingleOrDefaultAsync(booking => booking.Id == id, cancellationToken);

        if (booking is null)
        {
            return new DecideBookingResult.NotFound();
        }

        if (booking.Status != BookingStatus.Pending)
        {
            return new DecideBookingResult.NotPending(booking.Status);
        }

        // The database keeps microseconds, so the moment is cut to them and reads back the same.
        var now = clock.GetUtcNow();
        now = new DateTimeOffset(now.Ticks - (now.Ticks % 10), now.Offset);

        if (approve && booking.EndAt <= now)
        {
            return new DecideBookingResult.Expired();
        }

        booking.Status = approve ? BookingStatus.Confirmed : BookingStatus.Cancelled;
        booking.CancelledAt = approve ? null : now;
        booking.DecidedBy = user.FindFirstValue("sub");
        booking.DecidedAt = now;
        booking.DecisionReason = approve || string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        await db.SaveChangesAsync(cancellationToken);

        return new DecideBookingResult.Decided(BookingResponse.From(booking));
    }
}