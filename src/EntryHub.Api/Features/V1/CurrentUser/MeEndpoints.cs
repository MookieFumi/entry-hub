using EntryHub.Api.Features.V1.CurrentUser.GetMe;
using EntryHub.Api.Security;

namespace EntryHub.Api.Features.V1.CurrentUser;

public static class MeEndpoints
{
    public static void MapMeEndpoints(this RouteGroupBuilder v1) =>
        GetMeEndpoint.Map(v1.MapGroup("/me").WithTags("Me").RequireAuthorization(Policies.AnyEmployee));
}