using EntryHub.Api.Features.V1.Rooms.CreateRoom;
using EntryHub.Api.Features.V1.Rooms.GetRoom;
using EntryHub.Api.Features.V1.Rooms.ListRooms;
using EntryHub.Api.Features.V1.Rooms.StreamRoomOccupancy;
using EntryHub.Api.Security;

namespace EntryHub.Api.Features.V1.Rooms;

public static class RoomsEndpoints
{
    public static IServiceCollection AddRoomsFeature(this IServiceCollection services)
    {
        services.AddScoped<CreateRoomHandler>();
        services.AddScoped<GetRoomHandler>();
        services.AddScoped<ListRoomsHandler>();
        services.AddScoped<StreamRoomOccupancyHandler>();
        services.AddOptions<StreamRoomOccupancyOptions>();

        return services;
    }

    public static void MapRoomsEndpoints(this RouteGroupBuilder v1)
    {
        var rooms = v1.MapGroup("/rooms").WithTags("Rooms").RequireAuthorization(Policies.AnyEmployee);

        CreateRoomEndpoint.Map(rooms);
        GetRoomEndpoint.Map(rooms);
        ListRoomsEndpoint.Map(rooms);
        StreamRoomOccupancyEndpoint.Map(rooms);
    }
}