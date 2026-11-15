using System.Security.Claims;

using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Security;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace EntryHub.Api.Features.V1.Bookings.DecideBooking;

public static class DecideBookingEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/approve", ApproveAsync).WithName("ApproveBooking")
            .WithSummary("Confirms a pending booking. Facilities only. Anything but a pending booking gets 409.")
            .RequireAuthorization(Policies.Facilities)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces<RulesBrokenProblem>(StatusCodes.Status422UnprocessableEntity, "application/problem+json");

        group.MapPost("/{id:guid}/reject", RejectAsync).WithName("RejectBooking")
            .WithSummary("Rejects a pending booking: it is cancelled and the room is free again. The body with a reason is optional. Facilities only.")
            .RequireAuthorization(Policies.Facilities)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<Results<Ok<BookingResponse>, NotFound, ProblemHttpResult>> ApproveAsync(
        Guid id,
        ClaimsPrincipal user,
        DecideBookingHandler handler,
        CancellationToken cancellationToken) =>
        Respond(await handler.ApproveAsync(user, id, cancellationToken));

    private static async Task<Results<Ok<BookingResponse>, NotFound, ProblemHttpResult>> RejectAsync(
        Guid id,
        ClaimsPrincipal user,
        DecideBookingHandler handler,
        CancellationToken cancellationToken,
        [FromBody] RejectBookingRequest? request = null) =>
        Respond(await handler.RejectAsync(user, id, request?.Reason, cancellationToken));

    private static Results<Ok<BookingResponse>, NotFound, ProblemHttpResult> Respond(DecideBookingResult result) => result switch
    {
        DecideBookingResult.Decided decided => TypedResults.Ok(decided.Booking),
        DecideBookingResult.NotFound => TypedResults.NotFound(),
        DecideBookingResult.NotPending notPending => TypedResults.Problem(
            title: "The booking is not pending",
            detail: $"Only a pending booking can be approved or rejected; this one is {notPending.Status}.",
            statusCode: StatusCodes.Status409Conflict),
        DecideBookingResult.Expired => BookingProblems.RulesBroken(
            [new(BookingRuleCodes.NotActive, "The booking has already ended, so it can only be rejected.")]),
        _ => throw new InvalidOperationException($"Unhandled result {result.GetType().Name}."),
    };
}