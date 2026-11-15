using EntryHub.Api.Tenancy;

namespace EntryHub.Api.Domain.Sites;

/// <summary>
/// A date on which a site is closed: its rooms cannot be booked that day, whatever the opening hours say.
/// </summary>
public sealed class SiteHoliday : ITenantOwned
{
    public Guid Id { get; init; }

    public Guid TenantId { get; set; }

    public Guid SiteId { get; init; }

    /// <summary>
    /// The calendar day in the time zone of the site.
    /// </summary>
    public DateOnly Date { get; init; }

    public string? Name { get; init; }
}