using System.ComponentModel.DataAnnotations;

namespace EntryHub.Api.Features.V1.Sites.Holidays;

/// <param name="Date">The day the site is closed, in its own time zone (<c>yyyy-MM-dd</c>).</param>
/// <param name="Name">Optional label.</param>
public sealed record CreateHolidayRequest(DateOnly Date, [property: StringLength(100)] string? Name);

/// <param name="From">Only holidays on or after this day.</param>
/// <param name="To">Only holidays on or before this day.</param>
public sealed record ListHolidaysQuery(DateOnly? From, DateOnly? To);