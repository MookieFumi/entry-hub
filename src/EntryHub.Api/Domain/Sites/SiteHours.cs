namespace EntryHub.Api.Domain.Sites;

/// <summary>
/// When and where a site can be booked: the days and hours refer to <paramref name="TimeZoneId"/>.
/// </summary>
public sealed record SiteHours(string TimeZoneId, TimeOnly Opens, TimeOnly Closes, IReadOnlyList<DayOfWeek> OpenDays);