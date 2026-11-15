using EntryHub.Api.Data;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Features.V1.Sites.Holidays;

public sealed class ListHolidaysHandler(EntryHubDbContext db)
{
    /// <returns><see langword="null"/> when the site does not exist.</returns>
    public async Task<IReadOnlyList<SiteHolidayResponse>?> HandleAsync(Guid siteId, ListHolidaysQuery query, CancellationToken cancellationToken)
    {
        if (!await db.Sites.AnyAsync(site => site.Id == siteId, cancellationToken))
        {
            return null;
        }

        var holidays = db.SiteHolidays.AsNoTracking().Where(holiday => holiday.SiteId == siteId);

        if (query.From is { } from)
        {
            holidays = holidays.Where(holiday => holiday.Date >= from);
        }

        if (query.To is { } to)
        {
            holidays = holidays.Where(holiday => holiday.Date <= to);
        }

        return await holidays.OrderBy(holiday => holiday.Date).Select(holiday => SiteHolidayResponse.From(holiday)).ToListAsync(cancellationToken);
    }
}