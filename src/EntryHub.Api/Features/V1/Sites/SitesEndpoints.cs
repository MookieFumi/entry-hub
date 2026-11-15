using EntryHub.Api.Features.V1.Sites.CreateSite;
using EntryHub.Api.Features.V1.Sites.ListSites;
using EntryHub.Api.Features.V1.Sites.UpdateSite;
using EntryHub.Api.Security;

namespace EntryHub.Api.Features.V1.Sites;

public static class SitesEndpoints
{
    public static void MapSitesEndpoints(this RouteGroupBuilder v1)
    {
        var sites = v1.MapGroup("/sites").WithTags("Sites").RequireAuthorization(Policies.AnyEmployee);

        ListSitesEndpoint.Map(sites);
        CreateSiteEndpoint.Map(sites);
        UpdateSiteEndpoint.Map(sites);
    }
}