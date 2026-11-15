using System.Security.Claims;

using Microsoft.AspNetCore.Http.HttpResults;

namespace EntryHub.Api.Features.V1.Bookings.CheckInBooking;

public static class CheckInBookingEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/{id:guid}/check-in", HandleAsync).WithName("CheckInBooking")
            .WithSummary("Confirms that the people of a booking showed up. Allowed from 15 minutes before the start until the end, to the organizer and to Facilities. Checking in twice keeps the first moment.")
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<RulesBrokenProblem>(StatusCodes.Status422UnprocessableEntity, "application/problem+json");

    private static async Task<Results<Ok<BookingResponse>, NotFound, ForbidHttpResult, ProblemHttpResult>> HandleAsync(
        Guid id,
        ClaimsPrincipal user,
        CheckInBookingHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(user, id, cancellationToken);

        return result switch
        {
            CheckInBookingResult.CheckedIn checkedIn => TypedResults.Ok(checkedIn.Booking),
            CheckInBookingResult.NotFound => TypedResults.NotFound(),
            CheckInBookingResult.Forbidden => TypedResults.Forbid(),
            CheckInBookingResult.Refused refused => BookingProblems.RulesBroken([refused.Violation]),
            _ => throw new InvalidOperationException($"Unhandled result {result.GetType().Name}."),
        };
    }
}