using EntryHub.Api.Tenancy;

namespace EntryHub.Api.Domain.Sites;

/// <summary>
/// A place with rooms, such as an office. It has its own time zone, opening hours and open days.
/// </summary>
public sealed class Site : ITenantOwned
{
    public static readonly DayOfWeek[] WorkingWeek =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    public Guid Id { get; init; }

    public Guid TenantId { get; set; }

    public required string Name { get; set; }

    /// <summary>
    /// IANA time zone the hours and the open days refer to.
    /// </summary>
    public required string TimeZoneId { get; set; }

    public TimeOnly Opens { get; set; } = new(8, 0);

    public TimeOnly Closes { get; set; } = new(20, 0);

    public List<DayOfWeek> OpenDays { get; set; } = [.. WorkingWeek];

    /// <summary>
    /// A deactivated site and its rooms leave the listings and cannot be booked; the bookings they have stay.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; init; }

    public SiteHours Hours => new(TimeZoneId, Opens, Closes, OpenDays);
}