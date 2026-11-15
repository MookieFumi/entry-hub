using EntryHub.Api.Features.V1.Features;
using EntryHub.Api.Features.V1.Rooms.Blocks;
using EntryHub.Api.Features.V1.Rooms.CreateRoom;
using EntryHub.Api.Features.V1.Rooms.GetRoom;
using EntryHub.Api.Features.V1.Rooms.GetRoomSchedule;
using EntryHub.Api.Features.V1.Rooms.ListRooms;
using EntryHub.Api.Features.V1.Rooms.SearchAvailability;
using EntryHub.Api.Features.V1.Rooms.StreamRoomOccupancy;
using EntryHub.Api.Features.V1.Rooms.UpdateRoom;
using EntryHub.Api.Security;

namespace EntryHub.Api.Features.V1.Rooms;

public static class RoomsEndpoints
{
    public static IServiceCollection AddRoomsFeature(this IServiceCollection services)
    {
        services.AddOptions<StreamRoomOccupancyOptions>();
        services.AddScoped<FeatureResolver>();

        return services;
    }

    public static void MapRoomsEndpoints(this RouteGroupBuilder v1)
    {
        var rooms = v1.MapGroup("/rooms").WithTags("Rooms").RequireAuthorization(Policies.AnyEmployee);

        CreateRoomEndpoint.Map(rooms);
        UpdateRoomEndpoint.Map(rooms);
        GetRoomEndpoint.Map(rooms);
        SearchAvailabilityEndpoint.Map(rooms);
        GetRoomScheduleEndpoint.Map(rooms);
        ListRoomsEndpoint.Map(rooms);
        StreamRoomOccupancyEndpoint.Map(rooms);
        BlocksEndpoints.Map(rooms);
    }
}