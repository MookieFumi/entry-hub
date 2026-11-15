using EntryHub.Api.Domain.Features;
using EntryHub.Api.Domain.Sites;
using EntryHub.Api.Tenancy;

namespace EntryHub.Api.Domain.Rooms;

/// <summary>
/// A meeting room that employees can book.
/// </summary>
public sealed class Room : ITenantOwned
{
    public Guid Id { get; init; }

    public Guid TenantId { get; set; }

    public Guid SiteId { get; set; }

    public Site? Site { get; set; }

    public required string Name { get; set; }

    public int Floor { get; set; }

    public int Capacity { get; set; }

    /// <summary>
    /// What the room has, from the catalog of the tenant.
    /// </summary>
    public List<Feature> Features { get; set; } = [];

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; init; }
}