using EntryHub.Api.Domain.Bookings;

using Microsoft.AspNetCore.Http.HttpResults;

namespace EntryHub.Api.Features.V1.Bookings;

/// <summary>
/// The problem responses shared by the booking endpoints.
/// </summary>
internal static class BookingProblems
{
    public static ProblemHttpResult RulesBroken(IReadOnlyList<RuleViolation> violations) =>
        TypedResults.Problem(
            title: "The booking breaks the rules",
            detail: string.Join(" ", violations.Select(violation => violation.Message)),
            statusCode: StatusCodes.Status422UnprocessableEntity,
            extensions: new Dictionary<string, object?> { ["violations"] = violations });

    public static ProblemHttpResult Overlaps() =>
        TypedResults.Problem(
            title: "Room already booked",
            detail: "Another confirmed booking of this room overlaps the requested time.",
            statusCode: StatusCodes.Status409Conflict);
}