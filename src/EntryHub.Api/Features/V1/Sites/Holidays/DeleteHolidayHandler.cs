using EntryHub.Api.Data;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Features.V1.Sites.Holidays;

public sealed class DeleteHolidayHandler(EntryHubDbContext db)
{
    /// <returns><see langword="false"/> when the holiday does not exist in that site.</returns>
    public async Task<bool> HandleAsync(Guid siteId, Guid holidayId, CancellationToken cancellationToken)
    {
        var holiday = await db.SiteHolidays.SingleOrDefaultAsync(holiday => holiday.Id == holidayId && holiday.SiteId == siteId, cancellationToken);

        if (holiday is null)
        {
            return false;
        }

        db.SiteHolidays.Remove(holiday);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}