using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Domain.Rooms;
using EntryHub.Api.Domain.Tenants;
using EntryHub.Api.Tenancy;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Data;

/// <summary>
/// Database context of the application. Entity sets are added together with the feature that needs them.
/// </summary>
public sealed class EntryHubDbContext(DbContextOptions<EntryHubDbContext> options, ITenantContext? tenant = null) : DbContext(options)
{
    /// <summary>
    /// The tenant every query is filtered by. <see cref="Guid.Empty"/> when there is none, and no row has it,
    /// so a context without a tenant sees nothing instead of everything.
    /// </summary>
    internal Guid TenantId => tenant?.TenantId ?? Guid.Empty;

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<Room> Rooms => Set<Room>();

    public DbSet<Booking> Bookings => Set<Booking>();

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
        modelBuilder.Entity<Room>().HasQueryFilter(room => room.TenantId == TenantId);
        modelBuilder.Entity<Booking>().HasQueryFilter(booking => booking.TenantId == TenantId);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampTenant();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        StampTenant();
        return base.SaveChanges();
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