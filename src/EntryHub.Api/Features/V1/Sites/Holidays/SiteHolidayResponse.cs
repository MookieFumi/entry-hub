using EntryHub.Api.Domain.Sites;

namespace EntryHub.Api.Features.V1.Sites.Holidays;

/// <summary>
/// A day on which a site is closed.
/// </summary>
/// <param name="Id">Identifier of the holiday.</param>
/// <param name="SiteId">The site that is closed.</param>
/// <param name="Date">The day, in the time zone of the site.</param>
/// <param name="Name">What the day is, such as "New Year".</param>
public sealed record SiteHolidayResponse(Guid Id, Guid SiteId, DateOnly Date, string? Name)
{
    public static SiteHolidayResponse From(SiteHoliday holiday) => new(holiday.Id, holiday.SiteId, holiday.Date, holiday.Name);
}