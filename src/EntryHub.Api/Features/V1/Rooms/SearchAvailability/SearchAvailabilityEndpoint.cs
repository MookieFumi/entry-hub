using Microsoft.AspNetCore.Http.HttpResults;

namespace EntryHub.Api.Features.V1.Rooms.SearchAvailability;

public static class SearchAvailabilityEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/availability", HandleAsync).WithName("SearchRoomAvailability").WithSummary("Lists the rooms that are free for a time slot.")
            .ProducesValidationProblem();

    private static async Task<Results<Ok<IReadOnlyList<RoomResponse>>, ValidationProblem>> HandleAsync(
        [AsParameters] SearchAvailabilityQuery query,
        SearchAvailabilityHandler handler,
        CancellationToken cancellationToken)
    {
        if (ScheduleWindow.Validate(query.From, query.To) is { } errors)
        {
            return TypedResults.ValidationProblem(errors);
        }

        return TypedResults.Ok(await handler.HandleAsync(query, cancellationToken));
    }
}