using EntryHub.Api.Features.V1.Rules.GetRules;
using EntryHub.Api.Features.V1.Rules.UpdateRules;
using EntryHub.Api.Security;

namespace EntryHub.Api.Features.V1.Rules;

public static class RulesEndpoints
{
    public static void MapRulesEndpoints(this RouteGroupBuilder v1)
    {
        var rules = v1.MapGroup("/rules").WithTags("Rules").RequireAuthorization(Policies.AnyEmployee);

        GetRulesEndpoint.Map(rules);
        UpdateRulesEndpoint.Map(rules);
    }
}