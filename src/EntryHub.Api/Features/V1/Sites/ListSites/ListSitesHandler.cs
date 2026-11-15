using EntryHub.Api.Data;
using EntryHub.Api.Features.V1.Common;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Features.V1.Sites.ListSites;

public sealed class ListSitesHandler(EntryHubDbContext db)
{
    public async Task<IReadOnlyList<SiteResponse>> HandleAsync(Caller caller, ListSitesQuery query, CancellationToken cancellationToken)
    {
        var sites = db.Sites.AsNoTracking();

        if (!(caller.IsFacilities && query.IncludeInactive == true))
        {
            sites = sites.Where(site => site.IsActive);
        }

        var found = await sites.OrderBy(site => site.Name).ToListAsync(cancellationToken);

        return found.Select(SiteResponse.From).ToList();
    }
}