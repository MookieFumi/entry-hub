using EntryHub.Api.Data;
using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Domain.Sites;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EntryHub.Api.Features.V1.Rules;

/// <summary>
/// Finds the rules that apply to the tenant of the request: the ones it stored, or the configured defaults.
/// </summary>
public sealed class TenantRulesReader(EntryHubDbContext db, IOptions<BookingRulesOptions> defaults)
{
    public async Task<BookingRules> GetAsync(CancellationToken cancellationToken)
    {
        var stored = await db.TenantRules.AsNoTracking().SingleOrDefaultAsync(cancellationToken);

        return stored?.ToRules() ?? BookingRules.From(defaults.Value);
    }

    /// <summary>
    /// The hours of the site new clients should show when they ask for the rules without naming a site: the oldest
    /// active one, or the configured defaults when the tenant has none.
    /// </summary>
    public async Task<SiteHours> GetDefaultSiteHoursAsync(CancellationToken cancellationToken)
    {
        var site = await db.Sites.AsNoTracking()
            .Where(site => site.IsActive)
            .OrderBy(site => site.CreatedAt).ThenBy(site => site.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return site?.Hours ?? new SiteHours(
            defaults.Value.OfficeTimeZoneId,
            defaults.Value.OfficeOpens,
            defaults.Value.OfficeCloses,
            Site.WorkingWeek);
    }
}