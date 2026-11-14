using System.Security.Claims;

using EntryHub.Api.Features.V1.Common;

using Microsoft.AspNetCore.Http.HttpResults;

namespace EntryHub.Api.Features.V1.Rooms.GetRoomSchedule;

public static class GetRoomScheduleEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/{id:guid}/schedule", HandleAsync).WithName("GetRoomSchedule").WithSummary("Gets the busy ranges of a room in a time window.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static async Task<Results<Ok<IReadOnlyList<RoomScheduleEntry>>, ValidationProblem, ProblemHttpResult>> HandleAsync(
        Guid id,
        [AsParameters] GetRoomScheduleQuery query,
        ClaimsPrincipal user,
        GetRoomScheduleHandler handler,
        CancellationToken cancellationToken)
    {
        if (ScheduleWindow.Validate(query.From, query.To) is { } errors)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var schedule = await handler.HandleAsync(Caller.From(user), id, query, cancellationToken);

        return schedule is null
            ? TypedResults.Problem(title: "Room not found", statusCode: StatusCodes.Status404NotFound)
            : TypedResults.Ok(schedule);
    }
}