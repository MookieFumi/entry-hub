using EntryHub.Api.Security;

using Microsoft.AspNetCore.Http.HttpResults;

namespace EntryHub.Api.Features.V1.Features.UpdateFeature;

public static class UpdateFeatureEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPatch("/{id:guid}", HandleAsync).WithName("UpdateFeature").WithSummary("Renames a catalog entry or deactivates it. Facilities only.")
            .RequireAuthorization(Policies.Facilities)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

    private static async Task<Results<Ok<FeatureResponse>, ProblemHttpResult>> HandleAsync(
        Guid id,
        UpdateFeatureRequest request,
        UpdateFeatureHandler handler,
        CancellationToken cancellationToken)
    {
        var (outcome, feature) = await handler.HandleAsync(id, request, cancellationToken);

        return outcome switch
        {
            UpdateFeatureOutcome.Updated => TypedResults.Ok(feature!),
            UpdateFeatureOutcome.NameInUse => TypedResults.Problem(
                title: "Catalog entry already exists",
                detail: $"An entry named '{request.Name?.Trim()}' already exists.",
                statusCode: StatusCodes.Status409Conflict),
            _ => TypedResults.Problem(title: "Catalog entry not found", statusCode: StatusCodes.Status404NotFound),
        };
    }
}