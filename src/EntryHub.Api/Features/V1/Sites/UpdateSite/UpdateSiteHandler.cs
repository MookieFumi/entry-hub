using System.Globalization;

using EntryHub.Api.Data;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace EntryHub.Api.Features.V1.Sites.UpdateSite;

public enum UpdateSiteOutcome
{
    Updated,
    NotFound,
    Invalid,
    NameInUse,
}

public sealed class UpdateSiteHandler(EntryHubDbContext db)
{
    public async Task<(UpdateSiteOutcome Outcome, SiteResponse? Site, Dictionary<string, string[]>? Errors)> HandleAsync(
        Guid id,
        UpdateSiteRequest request,
        CancellationToken cancellationToken)
    {
        var site = await db.Sites.SingleOrDefaultAsync(site => site.Id == id, cancellationToken);

        if (site is null)
        {
            return (UpdateSiteOutcome.NotFound, null, null);
        }

        var errors = SiteInput.Validate(request.TimeZone, request.Opens, request.Closes, request.OpenDays, site);

        if (errors.Count > 0)
        {
            return (UpdateSiteOutcome.Invalid, null, errors);
        }

        if (request.Name?.Trim() is { Length: > 0 } name && name != site.Name)
        {
            if (await db.Sites.AnyAsync(other => other.Name == name && other.Id != id, cancellationToken))
            {
                return (UpdateSiteOutcome.NameInUse, null, null);
            }

            site.Name = name;
        }

        site.TimeZoneId = request.TimeZone ?? site.TimeZoneId;
        site.IsActive = request.IsActive ?? site.IsActive;

        if (request.Opens is not null)
        {
            site.Opens = TimeOnly.ParseExact(request.Opens, "HH:mm", CultureInfo.InvariantCulture);
        }

        if (request.Closes is not null)
        {
            site.Closes = TimeOnly.ParseExact(request.Closes, "HH:mm", CultureInfo.InvariantCulture);
        }

        if (request.OpenDays is not null)
        {
            site.OpenDays = [.. request.OpenDays.Distinct().Order()];
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return (UpdateSiteOutcome.NameInUse, null, null);
        }

        return (UpdateSiteOutcome.Updated, SiteResponse.From(site), null);
    }
}