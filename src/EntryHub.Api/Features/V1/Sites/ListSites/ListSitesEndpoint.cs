using System.Security.Claims;

using EntryHub.Api.Features.V1.Common;

using Microsoft.AspNetCore.Http.HttpResults;

namespace EntryHub.Api.Features.V1.Sites.ListSites;

public static class ListSitesEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/", HandleAsync).WithName("ListSites").WithSummary("Lists the sites of the tenant.");

    private static async Task<Ok<IReadOnlyList<SiteResponse>>> HandleAsync(
        ClaimsPrincipal user,
        [AsParameters] ListSitesQuery query,
        ListSitesHandler handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(Caller.From(user), query, cancellationToken));
}