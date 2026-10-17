using EntryHub.Api.Features.V1.Bookings;
using EntryHub.Api.Features.V1.Health.GetHealth;
using EntryHub.Api.Features.V1.Rooms;

namespace EntryHub.Api.Features.V1;

/// <summary>
/// Registers every slice of version 1 of the API under <c>/api/v1</c>.
/// </summary>
public static class V1Endpoints
{
    public static IServiceCollection AddV1Features(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<GetHealthHandler>();
        services.AddRoomsFeature();
        services.AddBookingsFeature(configuration);

        return services;
    }

    public static IEndpointRouteBuilder MapV1Endpoints(this IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/api/v1");

        GetHealthEndpoint.Map(v1);
        v1.MapRoomsEndpoints();
        v1.MapBookingsEndpoints();

        return app;
    }
}