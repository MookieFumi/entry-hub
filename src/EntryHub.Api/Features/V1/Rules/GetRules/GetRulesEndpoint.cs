using System.Security.Claims;

using EntryHub.Api.Features.V1.Common;

using Microsoft.AspNetCore.Http.HttpResults;

namespace EntryHub.Api.Features.V1.Rules.GetRules;

public static class GetRulesEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/", HandleAsync).WithName("GetBookingRules").WithSummary("Gets the booking rules of the caller's tenant.");

    private static async Task<Ok<RulesResponse>> HandleAsync(ClaimsPrincipal user, GetRulesHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(Caller.From(user), cancellationToken));
}