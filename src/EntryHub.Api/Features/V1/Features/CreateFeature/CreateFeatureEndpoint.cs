using EntryHub.Api.Security;

using Microsoft.AspNetCore.Http.HttpResults;

namespace EntryHub.Api.Features.V1.Features.CreateFeature;

public static class CreateFeatureEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/", HandleAsync).WithName("CreateFeature").WithSummary("Adds an entry to the catalog. Facilities only.")
            .RequireAuthorization(Policies.Facilities)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

    private static async Task<Results<Created<FeatureResponse>, ProblemHttpResult>> HandleAsync(
        CreateFeatureRequest request,
        CreateFeatureHandler handler,
        CancellationToken cancellationToken)
    {
        var (outcome, feature) = await handler.HandleAsync(request, cancellationToken);

        return outcome switch
        {
            CreateFeatureOutcome.Created => TypedResults.Created($"/api/v1/features/{feature!.Id}", feature),
            CreateFeatureOutcome.Invalid => TypedResults.Problem(
                title: "Invalid catalog entry",
                detail: "The name needs at least one letter or digit and the kind must be Equipment or Accessibility.",
                statusCode: StatusCodes.Status400BadRequest),
            _ => TypedResults.Problem(
                title: "Catalog entry already exists",
                detail: $"An entry named '{request.Name.Trim()}' already exists.",
                statusCode: StatusCodes.Status409Conflict),
        };
    }
}