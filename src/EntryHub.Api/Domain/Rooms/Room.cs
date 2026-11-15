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

    /// <summary>
    /// Minutes the room stays free before each booking, for setting up. Zero when it needs none.
    /// </summary>
    public int BufferBeforeMinutes { get; set; }

    /// <summary>
    /// Minutes the room stays free after each booking, for cleaning up. Zero when it needs none.
    /// </summary>
    public int BufferAfterMinutes { get; set; }

    /// <summary>
    /// When <see langword="true"/>, a booking made by an Employee waits as <c>Pending</c> until Facilities approves it.
    /// </summary>
    public bool RequiresApproval { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; init; }
}