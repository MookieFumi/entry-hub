using System.ComponentModel.DataAnnotations;

namespace EntryHub.Api.Features.V1.Rooms.CreateRoom;

/// <param name="Name">Unique among the rooms of the site.</param>
/// <param name="Floor">Floor of the room.</param>
/// <param name="Capacity">People the room holds.</param>
/// <param name="Equipment">Free-text equipment list.</param>
/// <param name="SiteId">Site of the room; the oldest active site of the tenant when left out.</param>
public sealed record CreateRoomRequest(
    [property: Required, StringLength(100)] string Name,
    [property: Range(-5, 100)] int Floor,
    [property: Range(1, 500)] int Capacity,
    List<string>? Equipment,
    Guid? SiteId = null);