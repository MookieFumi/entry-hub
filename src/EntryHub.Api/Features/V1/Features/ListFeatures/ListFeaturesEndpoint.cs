using System.Security.Claims;

using EntryHub.Api.Features.V1.Common;

using Microsoft.AspNetCore.Http.HttpResults;

namespace EntryHub.Api.Features.V1.Features.ListFeatures;

public static class ListFeaturesEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/", HandleAsync).WithName("ListFeatures").WithSummary("Lists the equipment and accessibility catalog of the tenant.");

    private static async Task<Ok<IReadOnlyList<FeatureResponse>>> HandleAsync(
        ClaimsPrincipal user,
        [AsParameters] ListFeaturesQuery query,
        ListFeaturesHandler handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(Caller.From(user), query, cancellationToken));
}