using EntryHub.Api.Security;

using Microsoft.AspNetCore.Http.HttpResults;

namespace EntryHub.Api.Features.V1.Rooms.CreateRoom;

public static class CreateRoomEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/", HandleAsync).WithName("CreateRoom").WithSummary("Creates a room. Facilities only.").RequireAuthorization(Policies.Facilities)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

    private static async Task<Results<Created<RoomResponse>, ProblemHttpResult>> HandleAsync(
        CreateRoomRequest request,
        CreateRoomHandler handler,
        CancellationToken cancellationToken)
    {
        var room = await handler.HandleAsync(request, cancellationToken);

        return room is null
            ? TypedResults.Problem(
                title: "Room name already in use",
                detail: $"A room named '{request.Name.Trim()}' already exists.",
                statusCode: StatusCodes.Status409Conflict)
            : TypedResults.Created($"/api/v1/rooms/{room.Id}", room);
    }
}