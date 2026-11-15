namespace EntryHub.Api.Domain.Bookings;

/// <summary>
/// The booking rules of one tenant, resolved for a request. The rules that depend on the place (time zone, hours and
/// days) are not here: they belong to the site of the room, see <see cref="Sites.SiteHours"/>.
/// </summary>
public sealed record BookingRules(
    TimeSpan MinDuration,
    TimeSpan MaxDuration,
    TimeSpan Slot,
    TimeSpan MinLeadTime,
    TimeSpan MaxAdvance,
    TimeSpan CancellationNotice,
    int MaxActiveBookingsPerEmployee,
    int AutoReleaseMinutes = 0)
{
    /// <summary>
    /// The rules of a tenant that has not stored its own: the values of the <c>BookingRules</c> configuration.
    /// </summary>
    public static BookingRules From(BookingRulesOptions options) => new(
        options.MinDuration,
        options.MaxDuration,
        options.Slot,
        options.MinLeadTime,
        options.MaxAdvance,
        options.CancellationNotice,
        options.MaxActiveBookingsPerEmployee);
}