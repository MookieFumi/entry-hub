using EntryHub.Api.Features.V1.Common;
using EntryHub.Api.Security;

using Microsoft.AspNetCore.Http.HttpResults;

namespace EntryHub.Api.Features.V1.Bookings.ListBookings;

public static class ListBookingsEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/", HandleAsync).WithName("ListBookings").WithSummary("Lists the bookings of every person, filtered, with paging. Facilities only.")
            .RequireAuthorization(Policies.Facilities);

    private static async Task<Ok<PagedResponse<BookingResponse>>> HandleAsync(
        [AsParameters] ListBookingsQuery query,
        ListBookingsHandler handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(query, cancellationToken));
}