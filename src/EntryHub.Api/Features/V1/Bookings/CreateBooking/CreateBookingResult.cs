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

    public sealed record Created(BookingResponse Booking) : CreateBookingResult;

    public sealed record RulesBroken(IReadOnlyList<RuleViolation> Violations) : CreateBookingResult;

    public sealed record Overlaps : CreateBookingResult;
}