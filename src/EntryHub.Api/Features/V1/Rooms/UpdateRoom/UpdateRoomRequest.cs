using System.ComponentModel.DataAnnotations;

namespace EntryHub.Api.Features.V1.Rooms.UpdateRoom;

/// <summary>
/// Fields left out are not changed. When <paramref name="Equipment"/> or <paramref name="FeatureIds"/> is sent, what the room has is replaced by both together.
/// </summary>
/// <param name="Name">New name; unique among the rooms of the site.</param>
/// <param name="Floor">New floor.</param>
/// <param name="Capacity">New capacity.</param>
/// <param name="Equipment">Names of what the room has; a name that matches an entry of the catalog (ignoring case) uses it, a new name adds an equipment entry.</param>
/// <param name="FeatureIds">Ids of active entries of the catalog the room has.</param>
/// <param name="SiteId">Move the room to another active site of the tenant.</param>
/// <param name="BufferBeforeMinutes">New minutes the room stays free before each booking; applies to bookings made or moved from now on.</param>
/// <param name="BufferAfterMinutes">New minutes the room stays free after each booking; applies to bookings made or moved from now on.</param>
/// <param name="IsActive"><see langword="false"/> takes the room out of the listings and out of new bookings; the bookings it already has stay.</param>
public sealed record UpdateRoomRequest(
    [property: StringLength(100, MinimumLength = 1)] string? Name,
    [property: Range(-5, 100)] int? Floor,
    [property: Range(1, 500)] int? Capacity,
    List<string>? Equipment,
    bool? IsActive,
    Guid? SiteId = null,
    List<Guid>? FeatureIds = null,
    [property: Range(0, 240)] int? BufferBeforeMinutes = null,
    [property: Range(0, 240)] int? BufferAfterMinutes = null);