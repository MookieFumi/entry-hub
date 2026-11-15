using System.ComponentModel.DataAnnotations;

namespace EntryHub.Api.Features.V1.Bookings.RescheduleBooking;

/// <summary>
/// Moves a booking to another time and, optionally, another room. The booking keeps its id, title, organizer and
/// attendees. The new time must pass the same rules as a new booking.
/// </summary>
/// <param name="StartAt">New start.</param>
/// <param name="EndAt">New end.</param>
/// <param name="RoomId">New room; the current one when left out.</param>
public sealed record RescheduleBookingRequest(DateTimeOffset StartAt, DateTimeOffset EndAt, Guid? RoomId = null) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndAt <= StartAt)
        {
            yield return new ValidationResult("EndAt must be after StartAt.", [nameof(EndAt)]);
        }
    }
}