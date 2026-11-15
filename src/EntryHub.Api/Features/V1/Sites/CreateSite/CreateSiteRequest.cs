using System.ComponentModel.DataAnnotations;

namespace EntryHub.Api.Features.V1.Sites.CreateSite;

/// <param name="Name">Unique among the sites of the tenant.</param>
/// <param name="TimeZone">IANA time zone, such as <c>Europe/Madrid</c>.</param>
/// <param name="Opens"><c>HH:mm</c>; 08:00 when left out.</param>
/// <param name="Closes"><c>HH:mm</c>; 20:00 when left out.</param>
/// <param name="OpenDays">Monday to Friday when left out.</param>
public sealed record CreateSiteRequest(
    [property: Required, StringLength(100, MinimumLength = 1)] string Name,
    [property: Required, StringLength(64)] string TimeZone,
    string? Opens,
    string? Closes,
    List<DayOfWeek>? OpenDays);