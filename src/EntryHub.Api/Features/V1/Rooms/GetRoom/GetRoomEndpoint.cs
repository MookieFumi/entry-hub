using Microsoft.AspNetCore.Http.HttpResults;

namespace EntryHub.Api.Features.V1.Rooms.GetRoom;

public static class GetRoomEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/{id:guid}", HandleAsync).WithName("GetRoom");

    private static async Task<Results<Ok<RoomResponse>, NotFound>> HandleAsync(
        Guid id,
        GetRoomHandler handler,
        CancellationToken cancellationToken)
    {
        var room = await handler.HandleAsync(id, cancellationToken);

        return room is null ? TypedResults.NotFound() : TypedResults.Ok(room);
    }
}