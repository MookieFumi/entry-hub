using EntryHub.Api.Domain.Audit;
using EntryHub.Api.Domain.Tenants;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EntryHub.Api.Data;

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("audit_entries");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).ValueGeneratedNever();
        builder.Property(entry => entry.ActorId).HasMaxLength(200);
        builder.Property(entry => entry.Action).HasMaxLength(20);
        builder.Property(entry => entry.EntityType).HasMaxLength(30);
        builder.Property(entry => entry.Summary).HasMaxLength(500);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(entry => entry.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entry => new { entry.TenantId, entry.At });
        builder.HasIndex(entry => new { entry.TenantId, entry.EntityType, entry.EntityId });
    }
}