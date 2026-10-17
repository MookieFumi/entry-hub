using EntryHub.Api.Features.V1.Common;

using Microsoft.AspNetCore.Http.HttpResults;

namespace EntryHub.Api.Features.V1.Rooms.ListRooms;

public static class ListRoomsEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/", HandleAsync).WithName("ListRooms");

    private static async Task<Ok<PagedResponse<RoomResponse>>> HandleAsync(
        [AsParameters] ListRoomsQuery query,
        ListRoomsHandler handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(query, cancellationToken));
}