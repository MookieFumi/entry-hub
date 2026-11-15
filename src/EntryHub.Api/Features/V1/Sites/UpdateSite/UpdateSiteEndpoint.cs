using EntryHub.Api.Security;

using Microsoft.AspNetCore.Http.HttpResults;

namespace EntryHub.Api.Features.V1.Sites.UpdateSite;

public static class UpdateSiteEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPatch("/{id:guid}", HandleAsync).WithName("UpdateSite").WithSummary("Edits a site or deactivates it. Facilities only.")
            .RequireAuthorization(Policies.Facilities)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

    private static async Task<Results<Ok<SiteResponse>, ValidationProblem, ProblemHttpResult>> HandleAsync(
        Guid id,
        UpdateSiteRequest request,
        UpdateSiteHandler handler,
        CancellationToken cancellationToken)
    {
        var (outcome, site, errors) = await handler.HandleAsync(id, request, cancellationToken);

        return outcome switch
        {
            UpdateSiteOutcome.Updated => TypedResults.Ok(site!),
            UpdateSiteOutcome.Invalid => TypedResults.ValidationProblem(errors!),
            UpdateSiteOutcome.NameInUse => TypedResults.Problem(
                title: "Site name already in use",
                detail: $"A site named '{request.Name?.Trim()}' already exists.",
                statusCode: StatusCodes.Status409Conflict),
            _ => TypedResults.Problem(title: "Site not found", statusCode: StatusCodes.Status404NotFound),
        };
    }
}