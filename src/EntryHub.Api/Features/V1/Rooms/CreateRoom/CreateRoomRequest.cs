using System.ComponentModel.DataAnnotations;

namespace EntryHub.Api.Features.V1.Rooms.CreateRoom;

/// <param name="Name">Unique among the rooms of the site.</param>
/// <param name="Floor">Floor of the room.</param>
/// <param name="Capacity">People the room holds.</param>
/// <param name="Equipment">Names of what the room has. A name that matches an entry of the catalog (ignoring case) uses it; a new name adds an equipment entry to the catalog.</param>
/// <param name="SiteId">Site of the room; the oldest active site of the tenant when left out.</param>
/// <param name="FeatureIds">Ids of active entries of the catalog the room has, added to the ones named in <paramref name="Equipment"/>.</param>
/// <param name="RequiresApproval">Bookings by Employees wait for approval; false when left out.</param>
/// <param name="BufferBeforeMinutes">Minutes the room stays free before each booking; none when left out.</param>
/// <param name="BufferAfterMinutes">Minutes the room stays free after each booking; none when left out.</param>
public sealed record CreateRoomRequest(
    [property: Required, StringLength(100)] string Name,
    [property: Range(-5, 100)] int Floor,
    [property: Range(1, 500)] int Capacity,
    List<string>? Equipment,
    Guid? SiteId = null,
    List<Guid>? FeatureIds = null,
    [property: Range(0, 240)] int BufferBeforeMinutes = 0,
    [property: Range(0, 240)] int BufferAfterMinutes = 0,
    bool RequiresApproval = false);