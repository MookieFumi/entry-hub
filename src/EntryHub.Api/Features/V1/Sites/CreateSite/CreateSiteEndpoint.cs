using EntryHub.Api.Security;

using Microsoft.AspNetCore.Http.HttpResults;

namespace EntryHub.Api.Features.V1.Sites.CreateSite;

public static class CreateSiteEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/", HandleAsync).WithName("CreateSite").WithSummary("Creates a site. Facilities only.")
            .RequireAuthorization(Policies.Facilities)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

    private static async Task<Results<Created<SiteResponse>, ValidationProblem, ProblemHttpResult>> HandleAsync(
        CreateSiteRequest request,
        CreateSiteHandler handler,
        CancellationToken cancellationToken)
    {
        var (outcome, site, errors) = await handler.HandleAsync(request, cancellationToken);

        return outcome switch
        {
            CreateSiteOutcome.Created => TypedResults.Created($"/api/v1/sites/{site!.Id}", site),
            CreateSiteOutcome.Invalid => TypedResults.ValidationProblem(errors!),
            _ => TypedResults.Problem(
                title: "Site name already in use",
                detail: $"A site named '{request.Name.Trim()}' already exists.",
                statusCode: StatusCodes.Status409Conflict),
        };
    }
}