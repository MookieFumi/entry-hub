using System.Security.Claims;

using EntryHub.Api.Data;
using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Features.V1.Common;
using EntryHub.Api.Features.V1.Rules;
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

    /// <summary>
    /// The outcome of cancelling with <see cref="CancelScope.Series"/>: some occurrences may be kept.
    /// </summary>
    public sealed record SeriesCancelled(CancelSeriesResponse Response) : CancelBookingResult;
}

/// <summary>
/// What a booking cancellation covers.
/// </summary>
public enum CancelScope
{
    /// <summary>
    /// Only the booking of the request.
    /// </summary>
    Occurrence,

    /// <summary>
    /// The booking and all the later occurrences of its series.
    /// </summary>
    Series,
}

/// <summary>
/// The result of cancelling a series from one occurrence on.
/// </summary>
/// <param name="Cancelled">Ids of the bookings that were cancelled (already cancelled ones are not listed).</param>
/// <param name="Kept">Occurrences that stay booked, each with the rule that kept it (for example, the cancellation notice).</param>
public sealed record CancelSeriesResponse(IReadOnlyList<Guid> Cancelled, IReadOnlyList<KeptOccurrence> Kept);

/// <param name="BookingId">The booking that was not cancelled.</param>
/// <param name="StartAt">When it starts.</param>
/// <param name="Violation">Why it was kept.</param>
public sealed record KeptOccurrence(Guid BookingId, DateTimeOffset StartAt, RuleViolation Violation);

public sealed class CancelBookingHandler(
    EntryHubDbContext db,
    BookingPolicy policy,
    TenantRulesReader rulesReader,
    TimeProvider clock,
    IAuthorizationService authorization)
{
    public async Task<CancelBookingResult> HandleAsync(ClaimsPrincipal user, Guid id, CancelScope scope, CancellationToken cancellationToken)
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

        var now = clock.GetUtcNow();
        var caller = Caller.From(user);

        if (scope == CancelScope.Series)
        {
            return await CancelSeriesAsync(booking, caller, now, cancellationToken);
        }

        if (booking.Status == BookingStatus.Cancelled)
        {
            return new CancelBookingResult.Cancelled();
        }

        var rules = await rulesReader.GetAsync(cancellationToken);

        if (policy.EvaluateCancellation(rules, booking.StartAt, now, caller.IsFacilities) is { } violation)
        {
            return new CancelBookingResult.TooLate(violation);
        }

        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAt = now;
        await db.SaveChangesAsync(cancellationToken);

        return new CancelBookingResult.Cancelled();
    }

    /// <summary>
    /// Cancels the booking and the later occurrences of its series that the cancellation notice still allows. All
    /// the occurrences of a series have the same organizer, so what the caller may do with one holds for the rest.
    /// </summary>
    private async Task<CancelBookingResult> CancelSeriesAsync(Booking booking, Caller caller, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var rules = await rulesReader.GetAsync(cancellationToken);

        var occurrences = booking.SeriesId is { } seriesId
            ? await db.Bookings
                .Where(other => other.SeriesId == seriesId && other.StartAt >= booking.StartAt && other.Status != BookingStatus.Cancelled)
                .OrderBy(other => other.StartAt)
                .ToListAsync(cancellationToken)
            : booking.Status == BookingStatus.Cancelled ? [] : [booking];

        var cancelled = new List<Guid>();
        var kept = new List<KeptOccurrence>();

        foreach (var occurrence in occurrences)
        {
            if (policy.EvaluateCancellation(rules, occurrence.StartAt, now, caller.IsFacilities) is { } violation)
            {
                kept.Add(new(occurrence.Id, occurrence.StartAt, violation));
                continue;
            }

            occurrence.Status = BookingStatus.Cancelled;
            occurrence.CancelledAt = now;
            cancelled.Add(occurrence.Id);
        }

        await db.SaveChangesAsync(cancellationToken);

        return new CancelBookingResult.SeriesCancelled(new(cancelled, kept));
    }
}