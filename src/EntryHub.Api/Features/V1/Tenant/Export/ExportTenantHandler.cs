using EntryHub.Api.Data;
using EntryHub.Api.Domain.Audit;
using EntryHub.Api.Features.V1.Audit;
using EntryHub.Api.Features.V1.Bookings;
using EntryHub.Api.Features.V1.Features;
using EntryHub.Api.Features.V1.Rooms;
using EntryHub.Api.Features.V1.Rooms.Blocks;
using EntryHub.Api.Features.V1.Sites;
using EntryHub.Api.Features.V1.Sites.Holidays;
using EntryHub.Api.Tenancy;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Features.V1.Tenant.Export;

public sealed class ExportTenantHandler(EntryHubDbContext db, ITenantContext tenantContext, ICurrentActor actor, TimeProvider clock)
{
    /// <summary>
    /// Reads everything of the tenant of the request, which the query filters limit to its own rows, and records the
    /// export in the audit trail. <see langword="null"/> when the tenant does not exist.
    /// </summary>
    public async Task<TenantExportResponse?> HandleAsync(CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var tenant = tenantId is null ? null : await db.Tenants.AsNoTracking().SingleOrDefaultAsync(tenant => tenant.Id == tenantId, cancellationToken);

        if (tenant is null)
        {
            return null;
        }

        var now = clock.GetUtcNow();
        var rules = await db.TenantRules.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        var sites = await db.Sites.AsNoTracking().OrderBy(site => site.CreatedAt).ThenBy(site => site.Id).ToListAsync(cancellationToken);
        var holidays = await db.SiteHolidays.AsNoTracking().OrderBy(holiday => holiday.Date).ThenBy(holiday => holiday.Id).ToListAsync(cancellationToken);
        var features = await db.Features.AsNoTracking().OrderBy(feature => feature.Code).ToListAsync(cancellationToken);
        var rooms = await db.Rooms.AsNoTracking().OrderBy(room => room.CreatedAt).ThenBy(room => room.Id).ToListAsync(cancellationToken);
        var blocks = await db.RoomBlocks.AsNoTracking().OrderBy(block => block.StartAt).ThenBy(block => block.Id).ToListAsync(cancellationToken);
        var bookings = await db.Bookings.AsNoTracking().OrderBy(booking => booking.StartAt).ThenBy(booking => booking.Id).ToListAsync(cancellationToken);
        var audit = await db.AuditEntries.AsNoTracking().OrderBy(entry => entry.At).ThenBy(entry => entry.Id).ToListAsync(cancellationToken);

        // The export is read-only, so nothing else writes this entry: it goes in on its own.
        if (actor.ActorId is { Length: > 0 } actorId)
        {
            db.AuditEntries.Add(new AuditEntry
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenant.Id,
                At = now,
                ActorId = actorId,
                Action = "Exported",
                EntityType = "Tenant",
                EntityId = tenant.Id,
                Summary = tenant.Name,
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        return new TenantExportResponse(
            tenant.Id,
            tenant.Name,
            now,
            rules is null ? null : new TenantRulesExport(
                rules.MinDurationMinutes,
                rules.MaxDurationMinutes,
                rules.SlotMinutes,
                rules.MinLeadTimeMinutes,
                rules.MaxAdvanceDays,
                rules.CancellationNoticeMinutes,
                rules.MaxActiveBookingsPerEmployee,
                rules.UpdatedAt),
            sites.Select(SiteResponse.From).ToList(),
            holidays.Select(SiteHolidayResponse.From).ToList(),
            features.Select(FeatureResponse.From).ToList(),
            rooms.Select(RoomResponse.From).ToList(),
            blocks.Select(RoomBlockResponse.From).ToList(),
            bookings.Select(BookingResponse.From).ToList(),
            audit.Select(AuditResponse.From).ToList());
    }
}