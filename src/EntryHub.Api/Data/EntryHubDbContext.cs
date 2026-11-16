using EntryHub.Api.Domain.Audit;
using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Domain.Features;
using EntryHub.Api.Domain.Rooms;
using EntryHub.Api.Domain.Sites;
using EntryHub.Api.Domain.Tenants;
using EntryHub.Api.Domain.Webhooks;
using EntryHub.Api.Tenancy;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Data;

/// <summary>
/// Database context of the application. Entity sets are added together with the feature that needs them.
/// </summary>
public sealed class EntryHubDbContext(
    DbContextOptions<EntryHubDbContext> options,
    ITenantContext? tenant = null,
    ICurrentActor? actor = null,
    TimeProvider? clock = null) : DbContext(options)
{
    /// <summary>
    /// The tenant every query is filtered by. <see cref="Guid.Empty"/> when there is none, and no row has it,
    /// so a context without a tenant sees nothing instead of everything.
    /// </summary>
    internal Guid TenantId => tenant?.TenantId ?? Guid.Empty;

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<Site> Sites => Set<Site>();

    public DbSet<SiteHoliday> SiteHolidays => Set<SiteHoliday>();

    public DbSet<TenantRules> TenantRules => Set<TenantRules>();

    public DbSet<Feature> Features => Set<Feature>();

    public DbSet<Room> Rooms => Set<Room>();

    public DbSet<RoomBlock> RoomBlocks => Set<RoomBlock>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    public DbSet<Booking> Bookings => Set<Booking>();

    public DbSet<BookingSeries> BookingSeries => Set<BookingSeries>();

    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();

    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();

    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        // One shared instance, so EF Core keeps reusing the same internal service provider.
        optionsBuilder.AddInterceptors(TenantConnectionInterceptor.Instance);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Needed so an exclusion constraint can combine equality on the room with overlap on the time range.
        modelBuilder.HasPostgresExtension("btree_gist");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EntryHubDbContext).Assembly);

        // The tenant filter lives here, once, and not in each query. Reading TenantId inside the lambda makes
        // EF Core evaluate it per context instance, not once per model.
        modelBuilder.Entity<Site>().HasQueryFilter(site => site.TenantId == TenantId);
        modelBuilder.Entity<SiteHoliday>().HasQueryFilter(holiday => holiday.TenantId == TenantId);
        modelBuilder.Entity<TenantRules>().HasQueryFilter(rules => rules.TenantId == TenantId);
        modelBuilder.Entity<Feature>().HasQueryFilter(feature => feature.TenantId == TenantId);
        modelBuilder.Entity<RoomFeature>().HasQueryFilter(link => link.TenantId == TenantId);
        modelBuilder.Entity<Room>().HasQueryFilter(room => room.TenantId == TenantId);
        modelBuilder.Entity<RoomBlock>().HasQueryFilter(block => block.TenantId == TenantId);
        modelBuilder.Entity<AuditEntry>().HasQueryFilter(entry => entry.TenantId == TenantId);
        modelBuilder.Entity<Booking>().HasQueryFilter(booking => booking.TenantId == TenantId);
        modelBuilder.Entity<BookingSeries>().HasQueryFilter(series => series.TenantId == TenantId);
        modelBuilder.Entity<WebhookSubscription>().HasQueryFilter(webhook => webhook.TenantId == TenantId);
        modelBuilder.Entity<WebhookDelivery>().HasQueryFilter(delivery => delivery.TenantId == TenantId);
        modelBuilder.Entity<IdempotencyRecord>().HasQueryFilter(record => record.TenantId == TenantId);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampBlockedRange();
        RecordAudit();
        await EnqueueWebhooksAsync(cancellationToken);
        StampTenant();
        return await base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        StampBlockedRange();
        RecordAudit();
        EnqueueWebhooksAsync(CancellationToken.None).GetAwaiter().GetResult();
        StampTenant();
        return base.SaveChanges();
    }

    /// <summary>
    /// Adds an audit entry for every change to something the trail covers, saved in the same transaction. Nothing is
    /// written when there is no actor, which is the case outside a request.
    /// </summary>
    private void RecordAudit()
    {
        if (actor?.ActorId is not { Length: > 0 } actorId || TenantId == Guid.Empty)
        {
            return;
        }

        var entries = AuditRecorder.Record([.. ChangeTracker.Entries()], actorId, TenantId, (clock ?? TimeProvider.System).GetUtcNow());
        AuditEntries.AddRange(entries);
    }

    /// <summary>
    /// Adds a delivery for each enabled subscription that wants an event caused by the pending booking changes, saved in
    /// the same transaction. Nothing is added when no booking changes or the tenant has no subscriptions.
    /// </summary>
    private async Task EnqueueWebhooksAsync(CancellationToken cancellationToken)
    {
        if (TenantId == Guid.Empty || ChangeTracker.Entries<Booking>().All(entry => entry.State is EntityState.Unchanged or EntityState.Detached))
        {
            return;
        }

        var events = WebhookOutbox.Detect([.. ChangeTracker.Entries()]);

        if (events.Count == 0)
        {
            return;
        }

        var subscriptions = await WebhookSubscriptions.AsNoTracking().Where(subscription => subscription.IsEnabled).ToListAsync(cancellationToken);

        if (subscriptions.Count > 0)
        {
            WebhookDeliveries.AddRange(WebhookOutbox.Build(events, subscriptions, (clock ?? TimeProvider.System).GetUtcNow()));
        }
    }

    /// <summary>
    /// Keeps the range that the exclusion constraint compares in step with the time and buffers of each booking.
    /// </summary>
    private void StampBlockedRange()
    {
        foreach (var entry in ChangeTracker.Entries<Booking>().Where(entry => entry.State is EntityState.Added or EntityState.Modified))
        {
            var booking = entry.Entity;
            booking.BlockedFrom = booking.StartAt.AddMinutes(-booking.BufferBeforeMinutes);
            booking.BlockedUntil = booking.EndAt.AddMinutes(booking.BufferAfterMinutes);
        }
    }

    private void StampTenant()
    {
        foreach (var entry in ChangeTracker.Entries<ITenantOwned>().Where(entry => entry.State == EntityState.Added))
        {
            if (entry.Entity.TenantId == Guid.Empty)
            {
                entry.Entity.TenantId = TenantId;
            }

            if (entry.Entity.TenantId == Guid.Empty || entry.Entity.TenantId != TenantId)
            {
                throw new InvalidOperationException("A row can only be written for the tenant of the current request.");
            }
        }
    }
}