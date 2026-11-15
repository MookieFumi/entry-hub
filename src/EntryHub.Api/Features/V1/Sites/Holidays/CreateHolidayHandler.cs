using EntryHub.Api.Data;
using EntryHub.Api.Domain.Sites;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace EntryHub.Api.Features.V1.Sites.Holidays;

public enum CreateHolidayOutcome
{
    Created,
    SiteNotFound,
    AlreadyClosed,
}

public sealed class CreateHolidayHandler(EntryHubDbContext db)
{
    public async Task<(CreateHolidayOutcome Outcome, SiteHolidayResponse? Holiday)> HandleAsync(
        Guid siteId,
        CreateHolidayRequest request,
        CancellationToken cancellationToken)
    {
        if (!await db.Sites.AnyAsync(site => site.Id == siteId, cancellationToken))
        {
            return (CreateHolidayOutcome.SiteNotFound, null);
        }

        if (await db.SiteHolidays.AnyAsync(holiday => holiday.SiteId == siteId && holiday.Date == request.Date, cancellationToken))
        {
            return (CreateHolidayOutcome.AlreadyClosed, null);
        }

        var holiday = new SiteHoliday
        {
            Id = Guid.CreateVersion7(),
            SiteId = siteId,
            Date = request.Date,
            Name = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim(),
        };

        db.SiteHolidays.Add(holiday);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return (CreateHolidayOutcome.AlreadyClosed, null);
        }

        return (CreateHolidayOutcome.Created, SiteHolidayResponse.From(holiday));
    }
}