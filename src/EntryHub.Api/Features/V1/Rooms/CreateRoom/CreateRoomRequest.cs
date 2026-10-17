using System.ComponentModel.DataAnnotations;

namespace EntryHub.Api.Features.V1.Rooms.CreateRoom;

public sealed record CreateRoomRequest(
    [property: Required, StringLength(100)] string Name,
    [property: Range(-5, 100)] int Floor,
    [property: Range(1, 500)] int Capacity,
    List<string>? Equipment);