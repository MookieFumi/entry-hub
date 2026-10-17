using System.Security.Claims;

using EntryHub.Api.Features.V1.Common;

using Microsoft.AspNetCore.Http.HttpResults;

namespace EntryHub.Api.Features.V1.Bookings.CreateBooking;

public static class CreateBookingEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/", HandleAsync).WithName("CreateBooking").WithSummary("Books a room, enforcing the booking rules.");

    private static async Task<Results<Created<BookingResponse>, ProblemHttpResult>> HandleAsync(
        CreateBookingRequest request,
        ClaimsPrincipal user,
        CreateBookingHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(Caller.From(user), request, cancellationToken);

        return result switch
        {
            CreateBookingResult.Created created =>
                TypedResults.Created($"/api/v1/bookings/{created.Booking.Id}", created.Booking),
            CreateBookingResult.RulesBroken broken => BookingProblems.RulesBroken(broken.Violations),
            CreateBookingResult.Overlaps => BookingProblems.Overlaps(),
            _ => throw new InvalidOperationException($"Unhandled result {result.GetType().Name}."),
        };
    }
}