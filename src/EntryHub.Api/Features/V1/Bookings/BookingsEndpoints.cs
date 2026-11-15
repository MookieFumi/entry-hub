using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Features.V1.Bookings.CancelBooking;
using EntryHub.Api.Features.V1.Bookings.CreateBooking;
using EntryHub.Api.Features.V1.Bookings.GetBooking;
using EntryHub.Api.Features.V1.Bookings.ListBookings;
using EntryHub.Api.Features.V1.Bookings.ListMyBookings;
using EntryHub.Api.Features.V1.Rules;
using EntryHub.Api.Security;

namespace EntryHub.Api.Features.V1.Bookings;

public static class BookingsEndpoints
{
    public static IServiceCollection AddBookingsFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<BookingRulesOptions>(configuration.GetSection("BookingRules"));
        services.AddSingleton<BookingPolicy>();
        services.AddScoped<TenantRulesReader>();

        return services;
    }

    public static void MapBookingsEndpoints(this RouteGroupBuilder v1)
    {
        var bookings = v1.MapGroup("/bookings").WithTags("Bookings").RequireAuthorization(Policies.AnyEmployee);

        CreateBookingEndpoint.Map(bookings);
        ListBookingsEndpoint.Map(bookings);
        ListMyBookingsEndpoint.Map(bookings);
        GetBookingEndpoint.Map(bookings);
        CancelBookingEndpoint.Map(bookings);
    }
}