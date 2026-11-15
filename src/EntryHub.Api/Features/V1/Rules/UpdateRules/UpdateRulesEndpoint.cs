using System.Security.Claims;

using EntryHub.Api.Features.V1.Common;
using EntryHub.Api.Features.V1.Rules.GetRules;
using EntryHub.Api.Security;

using Microsoft.AspNetCore.Http.HttpResults;

namespace EntryHub.Api.Features.V1.Rules.UpdateRules;

public static class UpdateRulesEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPut("/", HandleAsync).WithName("UpdateBookingRules")
            .WithSummary("Sets the booking rules of the caller's tenant. Facilities only. Applies to new bookings.")
            .RequireAuthorization(Policies.Facilities)
            .ProducesValidationProblem();

    private static async Task<Results<Ok<RulesResponse>, ValidationProblem>> HandleAsync(
        ClaimsPrincipal user,
        UpdateRulesRequest request,
        UpdateRulesHandler handler,
        GetRulesHandler get,
        CancellationToken cancellationToken)
    {
        if (await handler.HandleAsync(request, cancellationToken) is { } errors)
        {
            return TypedResults.ValidationProblem(errors);
        }

        return TypedResults.Ok(await get.HandleAsync(Caller.From(user), cancellationToken));
    }
}