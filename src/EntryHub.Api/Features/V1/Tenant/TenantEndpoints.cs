using EntryHub.Api.Features.V1.Tenant.Export;
using EntryHub.Api.Security;

namespace EntryHub.Api.Features.V1.Tenant;

public static class TenantEndpoints
{
    public static void MapTenantEndpoints(this RouteGroupBuilder v1)
    {
        var tenant = v1.MapGroup("/tenant").WithTags("Tenant").RequireAuthorization(Policies.Facilities);

        ExportTenantEndpoint.Map(tenant);
    }
}