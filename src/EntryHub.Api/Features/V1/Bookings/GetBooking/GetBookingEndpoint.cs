using System.Security.Claims;

using EntryHub.Api.Features.V1.Common;

using Microsoft.AspNetCore.Http.HttpResults;

namespace EntryHub.Api.Features.V1.Bookings.GetBooking;

public static class GetBookingEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/{id:guid}", HandleAsync).WithName("GetBooking").WithSummary("Gets a booking by id.")
            .Produces(StatusCodes.Status403Forbidden);

    private static async Task<Results<Ok<BookingResponse>, NotFound, ForbidHttpResult>> HandleAsync(
        Guid id,
        ClaimsPrincipal user,
        GetBookingHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(Caller.From(user), id, cancellationToken);

        return result switch
        {
            GetBookingResult.Found found => TypedResults.Ok(found.Booking),
            GetBookingResult.NotFound => TypedResults.NotFound(),
            GetBookingResult.Forbidden => TypedResults.Forbid(),
            _ => throw new InvalidOperationException($"Unhandled result {result.GetType().Name}."),
        };
    }
}