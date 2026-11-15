using EntryHub.Api.Security;

using Microsoft.AspNetCore.Http.HttpResults;

namespace EntryHub.Api.Features.V1.Rooms.UpdateRoom;

public static class UpdateRoomEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPatch("/{id:guid}", HandleAsync).WithName("UpdateRoom").WithSummary("Edits a room or deactivates it. Facilities only.")
            .RequireAuthorization(Policies.Facilities)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

    private static async Task<Results<Ok<RoomResponse>, ProblemHttpResult>> HandleAsync(
        Guid id,
        UpdateRoomRequest request,
        UpdateRoomHandler handler,
        CancellationToken cancellationToken)
    {
        var (outcome, room) = await handler.HandleAsync(id, request, cancellationToken);

        return outcome switch
        {
            UpdateRoomOutcome.Updated => TypedResults.Ok(room!),
            UpdateRoomOutcome.NameInUse => TypedResults.Problem(
                title: "Room name already in use",
                detail: $"A room named '{request.Name?.Trim()}' already exists.",
                statusCode: StatusCodes.Status409Conflict),
            UpdateRoomOutcome.SiteNotFound => TypedResults.Problem(
                title: "Site not found",
                detail: "The site does not exist or is not active.",
                statusCode: StatusCodes.Status400BadRequest),
            _ => TypedResults.Problem(title: "Room not found", statusCode: StatusCodes.Status404NotFound),
        };
    }
}