using System.ComponentModel.DataAnnotations;

namespace EntryHub.Api.Features.V1.Bookings.CreateBooking;

/// <summary>
/// The shape of the body. Rules that need data or a clock are checked later, in the handler.
/// </summary>
public sealed record CreateBookingRequest(
    Guid RoomId,
    [property: Required, StringLength(120)] string Title,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    [property: Range(1, 500)] int Attendees) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndAt <= StartAt)
        {
            yield return new ValidationResult("EndAt must be after StartAt.", [nameof(EndAt)]);
        }
    }
}