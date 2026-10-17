using System.Security.Claims;

using EntryHub.Api.Features.V1.Common;

using Microsoft.AspNetCore.Http.HttpResults;

namespace EntryHub.Api.Features.V1.Bookings.CancelBooking;

public static class CancelBookingEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/{id:guid}/cancel", HandleAsync).WithName("CancelBooking").WithSummary("Cancels a booking before it starts.");

    private static async Task<Results<NoContent, NotFound, ForbidHttpResult, ProblemHttpResult>> HandleAsync(
        Guid id,
        ClaimsPrincipal user,
        CancelBookingHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(Caller.From(user), id, cancellationToken);

        return result switch
        {
            CancelBookingResult.Cancelled => TypedResults.NoContent(),
            CancelBookingResult.NotFound => TypedResults.NotFound(),
            CancelBookingResult.Forbidden => TypedResults.Forbid(),
            CancelBookingResult.TooLate tooLate => BookingProblems.RulesBroken([tooLate.Violation]),
            _ => throw new InvalidOperationException($"Unhandled result {result.GetType().Name}."),
        };
    }
}