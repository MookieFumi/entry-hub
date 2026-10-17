using EntryHub.Api.Domain.Rooms;

namespace EntryHub.Api.Features.V1.Rooms;

public sealed record RoomResponse(
    Guid Id,
    string Name,
    int Floor,
    int Capacity,
    IReadOnlyList<string> Equipment,
    bool IsActive,
    DateTimeOffset CreatedAt)
{
    public static RoomResponse From(Room room) =>
        new(room.Id, room.Name, room.Floor, room.Capacity, room.Equipment, room.IsActive, room.CreatedAt);
}