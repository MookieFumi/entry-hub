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
        var (outcome, room) = await handler.HandleAsync(request, cancellationToken);

        return outcome switch
        {
            CreateRoomOutcome.Created => TypedResults.Created($"/api/v1/rooms/{room!.Id}", room),
            CreateRoomOutcome.SiteNotFound => TypedResults.Problem(
                title: "Site not found",
                detail: "The site does not exist, is not active, or the tenant has no site yet.",
                statusCode: StatusCodes.Status400BadRequest),
            _ => TypedResults.Problem(
                title: "Room name already in use",
                detail: $"A room named '{request.Name.Trim()}' already exists in the site.",
                statusCode: StatusCodes.Status409Conflict),
        };
    }
}