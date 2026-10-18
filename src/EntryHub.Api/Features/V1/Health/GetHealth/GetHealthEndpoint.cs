using Microsoft.AspNetCore.Http.HttpResults;

namespace EntryHub.Api.Features.V1.Health.GetHealth;

public static class GetHealthEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/health", HandleAsync).WithName("GetHealth").WithTags("Health").WithSummary("Reports whether the API and its database are reachable.")
            .WithDescription("Readiness for clients, as JSON. Kubernetes and the Aspire AppHost do not call this one: they use /health "
                + "(readiness) and /alive (liveness), described under the Probes tag, which sit outside the versioned API and answer plain text.");

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