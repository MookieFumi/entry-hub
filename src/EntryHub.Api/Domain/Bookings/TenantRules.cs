using EntryHub.Api.Tenancy;

namespace EntryHub.Api.Domain.Bookings;

/// <summary>
/// The booking rules a tenant chose for itself. One row per tenant; a tenant without a row uses the configured defaults.
/// </summary>
public sealed class TenantRules : ITenantOwned
{
    public Guid TenantId { get; set; }

    public int MinDurationMinutes { get; set; }

    public int MaxDurationMinutes { get; set; }

    public int SlotMinutes { get; set; }

    public int MinLeadTimeMinutes { get; set; }

    public int MaxAdvanceDays { get; set; }

    public int CancellationNoticeMinutes { get; set; }

    public int MaxActiveBookingsPerEmployee { get; set; }

    /// <summary>
    /// Minutes after the start of a confirmed booking that nobody checked in to before the reminders service cancels
    /// it and frees the room. Zero turns the release off.
    /// </summary>
    public int AutoReleaseMinutes { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public BookingRules ToRules() => new(
        TimeSpan.FromMinutes(MinDurationMinutes),
        TimeSpan.FromMinutes(MaxDurationMinutes),
        TimeSpan.FromMinutes(SlotMinutes),
        TimeSpan.FromMinutes(MinLeadTimeMinutes),
        TimeSpan.FromDays(MaxAdvanceDays),
        TimeSpan.FromMinutes(CancellationNoticeMinutes),
        MaxActiveBookingsPerEmployee,
        AutoReleaseMinutes);
}