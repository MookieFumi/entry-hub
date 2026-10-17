using EntryHub.Api.Domain.Rooms;

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
        builder.HasIndex(room => room.Name).IsUnique();
        builder.HasIndex(room => new { room.Floor, room.Capacity });
    }
}