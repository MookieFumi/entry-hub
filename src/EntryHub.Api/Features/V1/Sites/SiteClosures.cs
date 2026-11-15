using EntryHub.Api.Data;
using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Domain.Sites;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Features.V1.Sites;

/// <summary>
/// Asks whether a site is closed on the day of a booking. The booking policy has no database, so the handlers
/// that book or reschedule call this next to it.
/// </summary>
internal static class SiteClosures
{
    /// <returns>The violation when the site is closed that day, or <see langword="null"/> when it is open.</returns>
    public static async Task<RuleViolation?> CheckAsync(
        EntryHubDbContext db,
        Guid siteId,
        SiteHours hours,
        DateTimeOffset start,
        CancellationToken cancellationToken)
    {
        var day = LocalDay(hours.TimeZoneId, start);

        var holiday = await db.SiteHolidays.AsNoTracking()
            .Where(holiday => holiday.SiteId == siteId && holiday.Date == day)
            .Select(holiday => new { holiday.Name })
            .FirstOrDefaultAsync(cancellationToken);

        return holiday is null
            ? null
            : new(BookingRuleCodes.SiteClosed, $"The site is closed on {day:yyyy-MM-dd}{(holiday.Name is null ? "" : $" ({holiday.Name})")}.");
    }

    public static DateOnly LocalDay(string timeZoneId, DateTimeOffset moment) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(moment, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId)).DateTime);
}