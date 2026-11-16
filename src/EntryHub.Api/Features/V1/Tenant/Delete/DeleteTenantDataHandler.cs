using EntryHub.Api.Data;
using EntryHub.Api.Domain.Audit;
using EntryHub.Api.Domain.Features;
using EntryHub.Api.Tenancy;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Features.V1.Tenant.Delete;

public enum DeleteTenantDataOutcome
{
    Deleted,
    NotConfirmed,
    NotFound,
}

public sealed class DeleteTenantDataHandler(EntryHubDbContext db, ITenantContext tenantContext, ICurrentActor actor, TimeProvider clock)
{
    /// <summary>
    /// Removes every row of the tenant of the request in one transaction, children before parents, and leaves one audit
    /// entry that says who did it. The query filters and row-level security keep every statement inside the tenant.
    /// </summary>
    public async Task<(DeleteTenantDataOutcome Outcome, DeleteTenantDataResponse? Result)> HandleAsync(DeleteTenantDataRequest request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var tenant = tenantId is null ? null : await db.Tenants.AsNoTracking().SingleOrDefaultAsync(tenant => tenant.Id == tenantId, cancellationToken);

        if (tenant is null || actor.ActorId is not { Length: > 0 } actorId)
        {
            return (DeleteTenantDataOutcome.NotFound, null);
        }

        if (!string.Equals(request.Confirmation, $"DELETE {tenant.Name}", StringComparison.Ordinal))
        {
            return (DeleteTenantDataOutcome.NotConfirmed, null);
        }

        var now = clock.GetUtcNow();

        var result = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            await db.IdempotencyRecords.ExecuteDeleteAsync(cancellationToken);
            var bookings = await db.Bookings.ExecuteDeleteAsync(cancellationToken);
            await db.BookingSeries.ExecuteDeleteAsync(cancellationToken);
            await db.WebhookDeliveries.ExecuteDeleteAsync(cancellationToken);
            await db.WebhookSubscriptions.ExecuteDeleteAsync(cancellationToken);
            var blocks = await db.RoomBlocks.ExecuteDeleteAsync(cancellationToken);
            await db.Set<RoomFeature>().ExecuteDeleteAsync(cancellationToken);
            var rooms = await db.Rooms.IgnoreAutoIncludes().ExecuteDeleteAsync(cancellationToken);
            var holidays = await db.SiteHolidays.ExecuteDeleteAsync(cancellationToken);
            var features = await db.Features.ExecuteDeleteAsync(cancellationToken);
            await db.TenantRules.ExecuteDeleteAsync(cancellationToken);
            var sites = await db.Sites.ExecuteDeleteAsync(cancellationToken);
            var audit = await db.AuditEntries.ExecuteDeleteAsync(cancellationToken);

            db.AuditEntries.Add(new AuditEntry
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenant.Id,
                At = now,
                ActorId = actorId,
                Action = "Deleted",
                EntityType = "Tenant",
                EntityId = tenant.Id,
                Summary = "All data deleted",
            });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new DeleteTenantDataResponse(now, bookings, rooms, sites, features, blocks, holidays, audit);
        });

        return (DeleteTenantDataOutcome.Deleted, result);
    }
}