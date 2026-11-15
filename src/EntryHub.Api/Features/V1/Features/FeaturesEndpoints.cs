using EntryHub.Api.Features.V1.Features.CreateFeature;
using EntryHub.Api.Features.V1.Features.ListFeatures;
using EntryHub.Api.Features.V1.Features.UpdateFeature;
using EntryHub.Api.Security;

namespace EntryHub.Api.Features.V1.Features;

public static class FeaturesEndpoints
{
    public static void MapFeaturesEndpoints(this RouteGroupBuilder v1)
    {
        var features = v1.MapGroup("/features").WithTags("Features").RequireAuthorization(Policies.AnyEmployee);

        ListFeaturesEndpoint.Map(features);
        CreateFeatureEndpoint.Map(features);
        UpdateFeatureEndpoint.Map(features);
    }
}