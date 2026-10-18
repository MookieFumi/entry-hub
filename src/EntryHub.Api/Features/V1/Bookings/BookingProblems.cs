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

// Only for the contract: BookingProblems.RulesBroken writes these same fields, so clients learn about "violations".

/// <summary>
/// A booking that breaks one or more booking rules.
/// </summary>
/// <param name="Type">A URI that identifies the kind of problem.</param>
/// <param name="Title">A short summary of the problem.</param>
/// <param name="Status">Always 422.</param>
/// <param name="Detail">The messages of the broken rules, joined.</param>
/// <param name="Violations">Every rule the booking breaks, with a stable code.</param>
public sealed record RulesBrokenProblem(string Type, string Title, int Status, string Detail, IReadOnlyList<RuleViolation> Violations);