using EntryHub.Api.Domain.Bookings;

namespace EntryHub.Api.Features.V1.Bookings.RescheduleBooking;

public abstract record RescheduleBookingResult
{
    private RescheduleBookingResult()
    {
    }

    public sealed record Rescheduled(BookingResponse Booking) : RescheduleBookingResult;

    public sealed record NotFound : RescheduleBookingResult;

    public sealed record Forbidden : RescheduleBookingResult;

    public sealed record RulesBroken(IReadOnlyList<RuleViolation> Violations) : RescheduleBookingResult;

    public sealed record Overlaps : RescheduleBookingResult;
}