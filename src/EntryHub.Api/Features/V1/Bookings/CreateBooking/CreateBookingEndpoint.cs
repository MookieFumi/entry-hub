using System.Security.Claims;

using EntryHub.Api.Features.V1.Common;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace EntryHub.Api.Features.V1.Bookings.CreateBooking;

public static class CreateBookingEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/", HandleAsync).WithName("CreateBooking")
            .WithSummary("Books a room, enforcing the booking rules. Facilities can book for another user with onBehalfOf.")
            .WithDescription("Send an optional Idempotency-Key header to retry safely: the same key with the same body returns the "
                + "original booking (header Idempotent-Replayed: true) instead of creating another; the same key with a different "
                + "body returns 422. Keys are private to each user and are remembered for 24 hours.")
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem()
            .Produces<RulesBrokenProblem>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status400BadRequest);

    private static async Task<Results<Created<BookingResponse>, ForbidHttpResult, ProblemHttpResult>> HandleAsync(
        CreateBookingRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        HttpContext http,
        ClaimsPrincipal user,
        CreateBookingHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(Caller.From(user), request, idempotencyKey, cancellationToken);

        return result switch
        {
            CreateBookingResult.Created created => Created(http, created),
            CreateBookingResult.Forbidden => TypedResults.Forbid(),
            CreateBookingResult.KeyReused => BookingProblems.IdempotencyKeyReused(),
            CreateBookingResult.KeyInvalid => BookingProblems.IdempotencyKeyInvalid(),
            CreateBookingResult.RulesBroken broken => BookingProblems.RulesBroken(broken.Violations),
            CreateBookingResult.Overlaps => BookingProblems.Overlaps(),
            _ => throw new InvalidOperationException($"Unhandled result {result.GetType().Name}."),
        };
    }

    private static Created<BookingResponse> Created(HttpContext http, CreateBookingResult.Created created)
    {
        if (created.Replayed)
        {
            http.Response.Headers["Idempotent-Replayed"] = "true";
        }

        return TypedResults.Created($"/api/v1/bookings/{created.Booking.Id}", created.Booking);
    }
}