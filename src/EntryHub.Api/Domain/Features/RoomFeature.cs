using EntryHub.Api.Tenancy;

namespace EntryHub.Api.Domain.Features;

/// <summary>
/// The link between a room and an entry of the catalog. It carries the tenant so row-level security covers it too.
/// </summary>
public sealed class RoomFeature : ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid RoomId { get; set; }

    public Guid FeatureId { get; set; }
}