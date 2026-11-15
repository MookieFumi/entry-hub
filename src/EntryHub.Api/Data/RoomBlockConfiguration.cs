using EntryHub.Api.Domain.Rooms;
using EntryHub.Api.Domain.Tenants;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EntryHub.Api.Data;

internal sealed class RoomBlockConfiguration : IEntityTypeConfiguration<RoomBlock>
{
    public void Configure(EntityTypeBuilder<RoomBlock> builder)
    {
        builder.ToTable("room_blocks", table => table.HasCheckConstraint("ck_room_blocks_end_after_start", "\"EndAt\" > \"StartAt\""));
        builder.HasKey(block => block.Id);
        builder.Property(block => block.Id).ValueGeneratedNever();
        builder.Property(block => block.Reason).HasMaxLength(200);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(block => block.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Room>().WithMany().HasForeignKey(block => block.RoomId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(block => new { block.RoomId, block.StartAt, block.EndAt });
    }
}