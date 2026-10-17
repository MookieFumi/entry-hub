using EntryHub.Api.Domain.Rooms;

using Microsoft.AspNetCore.Http.HttpResults;

namespace EntryHub.Api.Features.V1.Rooms.StreamRoomOccupancy;

public static class StreamRoomOccupancyEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/{id:guid}/occupancy", HandleAsync).WithName("StreamRoomOccupancy");

    private static async Task<Results<ServerSentEventsResult<RoomOccupancy>, NotFound>> HandleAsync(
        Guid id,
        StreamRoomOccupancyHandler handler,
        CancellationToken cancellationToken)
    {
        var stream = await handler.HandleAsync(id, cancellationToken);

        return stream is null
            ? TypedResults.NotFound()
            : TypedResults.ServerSentEvents(stream, eventType: "occupancy");
    }
}