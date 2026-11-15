using EntryHub.Api.Data;
using EntryHub.Api.Features.V1.Common;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Features.V1.Features.ListFeatures;

public sealed class ListFeaturesHandler(EntryHubDbContext db)
{
    public async Task<IReadOnlyList<FeatureResponse>> HandleAsync(Caller caller, ListFeaturesQuery query, CancellationToken cancellationToken)
    {
        var features = db.Features.AsNoTracking();

        if (!(caller.IsFacilities && query.IncludeInactive == true))
        {
            features = features.Where(feature => feature.IsActive);
        }

        var found = await features.OrderBy(feature => feature.Kind).ThenBy(feature => feature.Name).ToListAsync(cancellationToken);

        return found.Select(FeatureResponse.From).ToList();
    }
}