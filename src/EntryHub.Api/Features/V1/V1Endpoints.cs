using EntryHub.Api.Features.V1.Audit;
using EntryHub.Api.Features.V1.Bookings;
using EntryHub.Api.Features.V1.CurrentUser;
using EntryHub.Api.Features.V1.Features;
using EntryHub.Api.Features.V1.Health.GetHealth;
using EntryHub.Api.Features.V1.Rooms;
using EntryHub.Api.Features.V1.Rules;
using EntryHub.Api.Features.V1.Sites;
using EntryHub.Api.Features.V1.Tenant;
using EntryHub.Api.Features.V1.Webhooks;

namespace EntryHub.Api.Features.V1;

/// <summary>
/// Registers every slice of version 1 of the API under <c>/api/v1</c>.
/// </summary>
public static class V1Endpoints
{
    public static IServiceCollection AddV1Features(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRoomsFeature();
        services.AddBookingsFeature(configuration);

        return services;
    }

    public static IEndpointRouteBuilder MapV1Endpoints(this IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/api/v1");
        v1.ProducesProblem(StatusCodes.Status429TooManyRequests);

        GetHealthEndpoint.Map(v1);
        v1.MapMeEndpoints();
        v1.MapRulesEndpoints();
        v1.MapSitesEndpoints();
        v1.MapFeaturesEndpoints();
        v1.MapRoomsEndpoints();
        v1.MapBookingsEndpoints();
        v1.MapAuditEndpoints();
        v1.MapTenantEndpoints();
        v1.MapWebhooksEndpoints();

        return app;
    }
}