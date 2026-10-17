using Microsoft.AspNetCore.Http.HttpResults;

namespace EntryHub.Api.Features.V1.Health.GetHealth;

public static class GetHealthEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/health", HandleAsync).WithName("GetHealth");

    private static async Task<Results<Ok<GetHealthResponse>, JsonHttpResult<GetHealthResponse>>> HandleAsync(
        GetHealthHandler handler,
        CancellationToken cancellationToken)
    {
        var response = await handler.HandleAsync(cancellationToken);

        return response.Status == "Healthy"
            ? TypedResults.Ok(response)
            : TypedResults.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}