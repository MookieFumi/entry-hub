using EntryHub.Api.Domain.Bookings;

namespace EntryHub.Api.Features.V1.Bookings.CreateBooking;

/// <summary>
/// What can happen when a booking is requested. A closed set of cases instead of exceptions.
/// </summary>
public abstract record CreateBookingResult
{
    private CreateBookingResult()
    {
    }

    /// <param name="Booking">The booking that was created, or the one created by the earlier request.</param>
    /// <param name="Replayed">True when the request repeated an earlier <c>Idempotency-Key</c> and nothing new was created.</param>
    public sealed record Created(BookingResponse Booking, bool Replayed = false) : CreateBookingResult;

    public sealed record RulesBroken(IReadOnlyList<RuleViolation> Violations) : CreateBookingResult;

    public sealed record Overlaps : CreateBookingResult;

    /// <summary>
    /// Only Facilities can book on behalf of someone else.
    /// </summary>
    public sealed record Forbidden : CreateBookingResult;

    /// <summary>
    /// The <c>Idempotency-Key</c> was already used with a different request.
    /// </summary>
    public sealed record KeyReused : CreateBookingResult;

    /// <summary>
    /// The <c>Idempotency-Key</c> is empty or longer than allowed.
    /// </summary>
    public sealed record KeyInvalid : CreateBookingResult;
}