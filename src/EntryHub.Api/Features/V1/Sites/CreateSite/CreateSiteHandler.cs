using EntryHub.Api.Data;
using EntryHub.Api.Domain.Sites;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace EntryHub.Api.Features.V1.Sites.CreateSite;

public enum CreateSiteOutcome
{
    Created,
    Invalid,
    NameInUse,
}

public sealed class CreateSiteHandler(EntryHubDbContext db, TimeProvider clock)
{
    public async Task<(CreateSiteOutcome Outcome, SiteResponse? Site, Dictionary<string, string[]>? Errors)> HandleAsync(
        CreateSiteRequest request,
        CancellationToken cancellationToken)
    {
        var errors = SiteInput.Validate(request.TimeZone, request.Opens, request.Closes, request.OpenDays, current: null);

        if (errors.Count > 0)
        {
            return (CreateSiteOutcome.Invalid, null, errors);
        }

        var name = request.Name.Trim();

        if (name.Length == 0)
        {
            return (CreateSiteOutcome.Invalid, null, new() { ["name"] = ["The name cannot be blank."] });
        }

        if (await db.Sites.AnyAsync(site => site.Name == name, cancellationToken))
        {
            return (CreateSiteOutcome.NameInUse, null, null);
        }

        var site = new Site
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            TimeZoneId = request.TimeZone,
            CreatedAt = clock.GetUtcNow(),
        };

        if (request.Opens is not null)
        {
            site.Opens = TimeOnly.ParseExact(request.Opens, "HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        }

        if (request.Closes is not null)
        {
            site.Closes = TimeOnly.ParseExact(request.Closes, "HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        }

        if (site.Closes <= site.Opens)
        {
            return (CreateSiteOutcome.Invalid, null, new() { ["closes"] = ["'closes' must be after 'opens'."] });
        }

        if (request.OpenDays is not null)
        {
            site.OpenDays = [.. request.OpenDays.Distinct().Order()];
        }

        db.Sites.Add(site);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return (CreateSiteOutcome.NameInUse, null, null);
        }

        return (CreateSiteOutcome.Created, SiteResponse.From(site), null);
    }
}