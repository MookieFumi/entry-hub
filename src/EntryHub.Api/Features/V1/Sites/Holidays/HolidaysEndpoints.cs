using EntryHub.Api.Security;

using Microsoft.AspNetCore.Http.HttpResults;

namespace EntryHub.Api.Features.V1.Sites.Holidays;

public static class HolidaysEndpoints
{
    public static void Map(RouteGroupBuilder sites)
    {
        sites.MapGet("/{siteId:guid}/holidays", ListAsync).WithName("ListSiteHolidays")
            .WithSummary("Lists the days a site is closed, optionally between two dates.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        sites.MapPost("/{siteId:guid}/holidays", CreateAsync).WithName("CreateSiteHoliday")
            .WithSummary("Closes a site on a day: its rooms cannot be booked that day. Facilities only.")
            .RequireAuthorization(Policies.Facilities)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        sites.MapDelete("/{siteId:guid}/holidays/{holidayId:guid}", DeleteAsync).WithName("DeleteSiteHoliday")
            .WithSummary("Opens the site again on that day. Existing bookings are not affected. Facilities only.")
            .RequireAuthorization(Policies.Facilities)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<Results<Ok<IReadOnlyList<SiteHolidayResponse>>, ProblemHttpResult>> ListAsync(
        Guid siteId,
        [AsParameters] ListHolidaysQuery query,
        ListHolidaysHandler handler,
        CancellationToken cancellationToken) =>
        await handler.HandleAsync(siteId, query, cancellationToken) is { } holidays
            ? TypedResults.Ok(holidays)
            : TypedResults.Problem(title: "Site not found", statusCode: StatusCodes.Status404NotFound);

    private static async Task<Results<Created<SiteHolidayResponse>, ProblemHttpResult>> CreateAsync(
        Guid siteId,
        CreateHolidayRequest request,
        CreateHolidayHandler handler,
        CancellationToken cancellationToken)
    {
        var (outcome, holiday) = await handler.HandleAsync(siteId, request, cancellationToken);

        return outcome switch
        {
            CreateHolidayOutcome.Created => TypedResults.Created($"/api/v1/sites/{siteId}/holidays/{holiday!.Id}", holiday),
            CreateHolidayOutcome.AlreadyClosed => TypedResults.Problem(
                title: "Day already closed",
                detail: $"The site is already closed on {request.Date:yyyy-MM-dd}.",
                statusCode: StatusCodes.Status409Conflict),
            _ => TypedResults.Problem(title: "Site not found", statusCode: StatusCodes.Status404NotFound),
        };
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteAsync(
        Guid siteId,
        Guid holidayId,
        DeleteHolidayHandler handler,
        CancellationToken cancellationToken) =>
        await handler.HandleAsync(siteId, holidayId, cancellationToken)
            ? TypedResults.NoContent()
            : TypedResults.Problem(title: "Holiday not found", statusCode: StatusCodes.Status404NotFound);
}