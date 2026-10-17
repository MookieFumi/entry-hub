namespace EntryHub.Api.Domain.Bookings;

/// <summary>
/// The business rules of a booking in one place. Bound from the <c>BookingRules</c> configuration section.
/// </summary>
public sealed class BookingRulesOptions
{
    public TimeSpan MinDuration { get; set; } = TimeSpan.FromMinutes(15);

    public TimeSpan MaxDuration { get; set; } = TimeSpan.FromHours(4);

    /// <summary>
    /// Bookings start and end on multiples of this length.
    /// </summary>
    public TimeSpan Slot { get; set; } = TimeSpan.FromMinutes(15);

    public TimeOnly OfficeOpens { get; set; } = new(8, 0);

    public TimeOnly OfficeCloses { get; set; } = new(20, 0);

    /// <summary>
    /// Office hours are Monday to Friday in this time zone.
    /// </summary>
    public string OfficeTimeZoneId { get; set; } = "Europe/Madrid";

    public TimeSpan MinLeadTime { get; set; } = TimeSpan.FromMinutes(15);

    public TimeSpan MaxAdvance { get; set; } = TimeSpan.FromDays(90);

    public TimeSpan CancellationNotice { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Active bookings an Employee can hold. Facilities has no limit.
    /// </summary>
    public int MaxActiveBookingsPerEmployee { get; set; } = 5;
}