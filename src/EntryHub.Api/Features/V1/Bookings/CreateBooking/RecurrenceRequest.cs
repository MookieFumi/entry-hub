using System.ComponentModel.DataAnnotations;

using EntryHub.Api.Domain.Bookings;

namespace EntryHub.Api.Features.V1.Bookings.CreateBooking;

/// <summary>
/// Makes the booking repeat. Send exactly one of <paramref name="Count"/> and <paramref name="Until"/>.
/// </summary>
/// <param name="Frequency">Daily or Weekly.</param>
/// <param name="Interval">Every how many days or weeks, from 1 to 52.</param>
/// <param name="Count">How many occurrences to create, including the first one (up to 52).</param>
/// <param name="Until">Last day, in the time zone of the site, on which an occurrence may start.</param>
public sealed record RecurrenceRequest(
    RecurrenceFrequency Frequency,
    [property: Range(1, 52)] int Interval = 1,
    [property: Range(1, BookingSeries.MaxOccurrences)] int? Count = null,
    DateOnly? Until = null) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if ((Count is null) == (Until is null))
        {
            yield return new ValidationResult("Send exactly one of Count and Until.", [nameof(Count), nameof(Until)]);
        }
    }
}