namespace EntryHub.Api.Domain.Bookings;

/// <param name="Code">Stable identifier that clients can switch on.</param>
/// <param name="Message">Explanation for a person.</param>
public sealed record RuleViolation(string Code, string Message);

public static class BookingRuleCodes
{
    public const string DurationTooShort = nameof(DurationTooShort);
    public const string DurationTooLong = nameof(DurationTooLong);
    public const string NotOnSlotBoundary = nameof(NotOnSlotBoundary);
    public const string OutsideOfficeHours = nameof(OutsideOfficeHours);
    public const string TooSoon = nameof(TooSoon);
    public const string TooFarAhead = nameof(TooFarAhead);
    public const string ExceedsCapacity = nameof(ExceedsCapacity);
    public const string RoomUnavailable = nameof(RoomUnavailable);
    public const string TooManyActiveBookings = nameof(TooManyActiveBookings);
    public const string TooLateToCancel = nameof(TooLateToCancel);
}