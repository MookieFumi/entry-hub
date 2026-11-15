using System.Security.Claims;

using Microsoft.AspNetCore.Http.HttpResults;

namespace EntryHub.Api.Features.V1.Bookings.RescheduleBooking;

public static class RescheduleBookingEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/{id:guid}/reschedule", HandleAsync).WithName("RescheduleBooking")
            .WithSummary("Moves a booking to another time or room, keeping its id. Organizer or Facilities.")
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<RulesBrokenProblem>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")
            .ProducesProblem(StatusCodes.Status409Conflict);

    private static async Task<Results<Ok<BookingResponse>, NotFound, ForbidHttpResult, ProblemHttpResult>> HandleAsync(
        Guid id,
        RescheduleBookingRequest request,
        ClaimsPrincipal user,
        RescheduleBookingHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(user, id, request, cancellationToken);

        return result switch
        {
            RescheduleBookingResult.Rescheduled moved => TypedResults.Ok(moved.Booking),
            RescheduleBookingResult.NotFound => TypedResults.NotFound(),
            RescheduleBookingResult.Forbidden => TypedResults.Forbid(),
            RescheduleBookingResult.RulesBroken broken => BookingProblems.RulesBroken(broken.Violations),
            RescheduleBookingResult.Overlaps => BookingProblems.Overlaps(),
            _ => throw new InvalidOperationException($"Unhandled result {result.GetType().Name}."),
        };
    }
}