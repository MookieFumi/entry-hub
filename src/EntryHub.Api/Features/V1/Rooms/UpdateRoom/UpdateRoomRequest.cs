using System.ComponentModel.DataAnnotations;

namespace EntryHub.Api.Features.V1.Rooms.UpdateRoom;

/// <summary>
/// Fields left out are not changed. <paramref name="Equipment"/> replaces the whole list when it is sent.
/// </summary>
/// <param name="Name">New name; unique among the rooms of the site.</param>
/// <param name="Floor">New floor.</param>
/// <param name="Capacity">New capacity.</param>
/// <param name="Equipment">New equipment list.</param>
/// <param name="SiteId">Move the room to another active site of the tenant.</param>
/// <param name="IsActive"><see langword="false"/> takes the room out of the listings and out of new bookings; the bookings it already has stay.</param>
public sealed record UpdateRoomRequest(
    [property: StringLength(100, MinimumLength = 1)] string? Name,
    [property: Range(-5, 100)] int? Floor,
    [property: Range(1, 500)] int? Capacity,
    List<string>? Equipment,
    bool? IsActive,
    Guid? SiteId = null);