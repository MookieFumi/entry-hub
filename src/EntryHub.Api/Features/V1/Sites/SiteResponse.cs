using System.Globalization;

using EntryHub.Api.Domain.Sites;

namespace EntryHub.Api.Features.V1.Sites;

/// <summary>
/// A place with rooms. The hours and the days refer to <paramref name="TimeZone"/>.
/// </summary>
/// <param name="Id">Identifier of the site.</param>
/// <param name="Name">Unique among the sites of the tenant.</param>
/// <param name="TimeZone">IANA time zone, such as <c>Europe/Madrid</c>.</param>
/// <param name="Opens">First start time of the day, <c>HH:mm</c>.</param>
/// <param name="Closes">Last end time of the day, <c>HH:mm</c>.</param>
/// <param name="OpenDays">Days of the week the rooms of the site can be booked.</param>
/// <param name="IsActive">A deactivated site and its rooms are not listed and cannot be booked.</param>
/// <param name="CreatedAt">When the site was created.</param>
public sealed record SiteResponse(
    Guid Id,
    string Name,
    string TimeZone,
    string Opens,
    string Closes,
    IReadOnlyList<DayOfWeek> OpenDays,
    bool IsActive,
    DateTimeOffset CreatedAt)
{
    public static SiteResponse From(Site site) => new(
        site.Id,
        site.Name,
        site.TimeZoneId,
        site.Opens.ToString("HH\\:mm", CultureInfo.InvariantCulture),
        site.Closes.ToString("HH\\:mm", CultureInfo.InvariantCulture),
        site.OpenDays.Order().ToList(),
        site.IsActive,
        site.CreatedAt);
}