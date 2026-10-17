using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Domain.Rooms;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Data;

/// <summary>
/// Database context of the application. Entity sets are added together with the feature that needs them.
/// </summary>
public sealed class EntryHubDbContext(DbContextOptions<EntryHubDbContext> options) : DbContext(options)
{
    public DbSet<Room> Rooms => Set<Room>();

    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Needed so an exclusion constraint can combine equality on the room with overlap on the time range.
        modelBuilder.HasPostgresExtension("btree_gist");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EntryHubDbContext).Assembly);
    }
}