using System.ComponentModel.DataAnnotations;

namespace EntryHub.Api.Features.V1.Rooms.CreateRoom;

/// <param name="Name">Unique among the rooms of the site.</param>
/// <param name="Floor">Floor of the room.</param>
/// <param name="Capacity">People the room holds.</param>
/// <param name="Equipment">Names of what the room has. A name that matches an entry of the catalog (ignoring case) uses it; a new name adds an equipment entry to the catalog.</param>
/// <param name="SiteId">Site of the room; the oldest active site of the tenant when left out.</param>
/// <param name="FeatureIds">Ids of active entries of the catalog the room has, added to the ones named in <paramref name="Equipment"/>.</param>
public sealed record CreateRoomRequest(
    [property: Required, StringLength(100)] string Name,
    [property: Range(-5, 100)] int Floor,
    [property: Range(1, 500)] int Capacity,
    List<string>? Equipment,
    Guid? SiteId = null,
    List<Guid>? FeatureIds = null);