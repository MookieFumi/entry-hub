namespace EntryHub.Reminders;

/// <summary>
/// Settings under the <c>Reminders</c> configuration section.
/// </summary>
public sealed class ReminderOptions
{
    public const string SectionName = "Reminders";

    /// <summary>
    /// How long before the start of a booking its organizer is reminded.
    /// </summary>
    public TimeSpan LeadTime { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// How often the service looks for bookings that are about to start.
    /// </summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long a reminder that was picked up stays reserved. If sending it fails, the next try is after this time.
    /// </summary>
    public TimeSpan RetryAfter { get; set; } = TimeSpan.FromMinutes(1);
}