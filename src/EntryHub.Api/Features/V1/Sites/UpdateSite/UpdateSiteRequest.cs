using System.ComponentModel.DataAnnotations;

namespace EntryHub.Api.Features.V1.Sites.UpdateSite;

/// <summary>
/// Fields left out are not changed. <paramref name="OpenDays"/> replaces the whole list when it is sent.
/// </summary>
/// <param name="Name">New name; unique among the sites of the tenant.</param>
/// <param name="TimeZone">New IANA time zone.</param>
/// <param name="Opens">New opening time, <c>HH:mm</c>.</param>
/// <param name="Closes">New closing time, <c>HH:mm</c>.</param>
/// <param name="OpenDays">New open days.</param>
/// <param name="IsActive"><see langword="false"/> takes the site and its rooms out of the listings and out of new bookings; the bookings they already have stay.</param>
public sealed record UpdateSiteRequest(
    [property: StringLength(100, MinimumLength = 1)] string? Name,
    [property: StringLength(64)] string? TimeZone,
    string? Opens,
    string? Closes,
    List<DayOfWeek>? OpenDays,
    bool? IsActive);