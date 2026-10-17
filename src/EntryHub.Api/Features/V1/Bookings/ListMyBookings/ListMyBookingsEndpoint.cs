using System.Security.Claims;

using EntryHub.Api.Features.V1.Common;

using Microsoft.AspNetCore.Http.HttpResults;

namespace EntryHub.Api.Features.V1.Bookings.ListMyBookings;

public static class ListMyBookingsEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/mine", HandleAsync).WithName("ListMyBookings");

    private static async Task<Ok<PagedResponse<BookingResponse>>> HandleAsync(
        [AsParameters] ListMyBookingsQuery query,
        ClaimsPrincipal user,
        ListMyBookingsHandler handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(Caller.From(user), query, cancellationToken));
}