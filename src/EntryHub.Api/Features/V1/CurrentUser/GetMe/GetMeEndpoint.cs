using System.Security.Claims;

using Microsoft.AspNetCore.Http.HttpResults;

namespace EntryHub.Api.Features.V1.CurrentUser.GetMe;

public static class GetMeEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/", HandleAsync).WithName("GetMe").WithSummary("Says who is calling: user, role and tenant.")
            .Produces(StatusCodes.Status403Forbidden);

    private static async Task<Results<Ok<MeResponse>, ForbidHttpResult>> HandleAsync(
        ClaimsPrincipal user,
        GetMeHandler handler,
        CancellationToken cancellationToken)
    {
        var me = await handler.HandleAsync(user, cancellationToken);

        // A token for a tenant that does not exist is as good as a token without one.
        return me is null ? TypedResults.Forbid() : TypedResults.Ok(me);
    }
}