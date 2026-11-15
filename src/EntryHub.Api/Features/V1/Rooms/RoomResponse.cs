using EntryHub.Api.Domain.Rooms;
using EntryHub.Api.Features.V1.Features;

namespace EntryHub.Api.Features.V1.Rooms;

/// <param name="Id">Identifier of the room.</param>
/// <param name="SiteId">Site the room belongs to.</param>
/// <param name="Name">Unique among the rooms of the site.</param>
/// <param name="Floor">Floor of the room.</param>
/// <param name="Capacity">People the room holds.</param>
/// <param name="Equipment">Names of what the room has, the same as in <paramref name="Features"/>; kept for clients that only show text.</param>
/// <param name="Features">What the room has, from the catalog of the tenant.</param>
/// <param name="BufferBeforeMinutes">Minutes the room stays free before each booking.</param>
/// <param name="BufferAfterMinutes">Minutes the room stays free after each booking.</param>
/// <param name="RequiresApproval">A booking of this room made by an Employee waits for approval by Facilities.</param>
/// <param name="IsActive">A deactivated room is not listed and cannot be booked.</param>
/// <param name="CreatedAt">When the room was created.</param>
public sealed record RoomResponse(
    Guid Id,
    Guid SiteId,
    string Name,
    int Floor,
    int Capacity,
    IReadOnlyList<string> Equipment,
    IReadOnlyList<FeatureResponse> Features,
    int BufferBeforeMinutes,
    int BufferAfterMinutes,
    bool RequiresApproval,
    bool IsActive,
    DateTimeOffset CreatedAt)
{
    public static RoomResponse From(Room room)
    {
        var features = room.Features.OrderBy(feature => feature.Kind).ThenBy(feature => feature.Name, StringComparer.Ordinal).ToList();

        return new(
            room.Id,
            room.SiteId,
            room.Name,
            room.Floor,
            room.Capacity,
            features.Select(feature => feature.Name).ToList(),
            features.Select(FeatureResponse.From).ToList(),
            room.BufferBeforeMinutes,
            room.BufferAfterMinutes,
            room.RequiresApproval,
            room.IsActive,
            room.CreatedAt);
    }
}