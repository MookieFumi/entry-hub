using System.Security.Claims;

using EntryHub.Api.Features.V1.Common;

using Microsoft.AspNetCore.Http.HttpResults;

namespace EntryHub.Api.Features.V1.Rules.GetRules;

public static class GetRulesEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/", HandleAsync).WithName("GetBookingRules").WithSummary("Gets the booking rules that apply to the caller.");

    private static Ok<RulesResponse> HandleAsync(ClaimsPrincipal user, GetRulesHandler handler) =>
        TypedResults.Ok(handler.Handle(Caller.From(user)));
}