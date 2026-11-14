using EntryHub.Api.Features.V1.Rules.GetRules;
using EntryHub.Api.Security;

namespace EntryHub.Api.Features.V1.Rules;

public static class RulesEndpoints
{
    public static void MapRulesEndpoints(this RouteGroupBuilder v1) =>
        GetRulesEndpoint.Map(v1.MapGroup("/rules").WithTags("Rules").RequireAuthorization(Policies.AnyEmployee));
}