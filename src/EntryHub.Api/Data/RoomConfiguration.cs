using EntryHub.Api.Domain.Features;
using EntryHub.Api.Domain.Rooms;
using EntryHub.Api.Domain.Tenants;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EntryHub.Api.Data;

internal sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("rooms");
        builder.HasKey(room => room.Id);
        builder.Property(room => room.Id).ValueGeneratedNever();
        builder.Property(room => room.Name).HasMaxLength(100);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(room => room.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(room => room.Site).WithMany().HasForeignKey(room => room.SiteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(room => room.Features).WithMany()
            .UsingEntity<RoomFeature>(
                link => link.HasOne<Feature>().WithMany().HasForeignKey(item => item.FeatureId).OnDelete(DeleteBehavior.Restrict),
                link => link.HasOne<Room>().WithMany().HasForeignKey(item => item.RoomId).OnDelete(DeleteBehavior.Cascade),
                link =>
                {
                    link.ToTable("room_features");
                    link.HasKey(item => new { item.RoomId, item.FeatureId });
                });
        // Every read of a room returns what it has, so a response never forgets its features.
        builder.Navigation(room => room.Features).AutoInclude();
        // Names are unique inside a site: two offices, or two customers, can both have a "Boardroom".
        builder.HasIndex(room => new { room.SiteId, room.Name }).IsUnique();
        builder.HasIndex(room => new { room.TenantId, room.Floor, room.Capacity });
    }
}