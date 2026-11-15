using System.Security.Claims;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace EntryHub.Api.Features.V1.Bookings.CancelBooking;

public static class CancelBookingEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/{id:guid}/cancel", HandleAsync).WithName("CancelBooking").WithSummary("Cancels a booking before it starts.")
            .WithDescription("With scope=series (the value is not case-sensitive), the booking and all the later occurrences of its series are cancelled, the ones that "
                + "the cancellation notice no longer allows are kept, and the response (200) lists both. Without it, or with "
                + "scope=occurrence, only this booking is cancelled (204).")
            .Produces<CancelSeriesResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<RulesBrokenProblem>(StatusCodes.Status422UnprocessableEntity, "application/problem+json");

    private static async Task<Results<NoContent, Ok<CancelSeriesResponse>, NotFound, ForbidHttpResult, ProblemHttpResult>> HandleAsync(
        Guid id,
        [FromQuery] string? scope,
        ClaimsPrincipal user,
        CancelBookingHandler handler,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<CancelScope>(scope ?? nameof(CancelScope.Occurrence), ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
        {
            return TypedResults.Problem(
                title: "Invalid scope",
                detail: "The scope must be occurrence or series.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await handler.HandleAsync(user, id, parsed, cancellationToken);

        return result switch
        {
            CancelBookingResult.Cancelled => TypedResults.NoContent(),
            CancelBookingResult.SeriesCancelled series => TypedResults.Ok(series.Response),
            CancelBookingResult.NotFound => TypedResults.NotFound(),
            CancelBookingResult.Forbidden => TypedResults.Forbid(),
            CancelBookingResult.TooLate tooLate => BookingProblems.RulesBroken([tooLate.Violation]),
            _ => throw new InvalidOperationException($"Unhandled result {result.GetType().Name}."),
        };
    }
}