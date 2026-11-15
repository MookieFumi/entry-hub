using EntryHub.Api.Domain.Features;
using EntryHub.Api.Domain.Tenants;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EntryHub.Api.Data;

internal sealed class FeatureConfiguration : IEntityTypeConfiguration<Feature>
{
    public void Configure(EntityTypeBuilder<Feature> builder)
    {
        builder.ToTable("features");
        builder.HasKey(feature => feature.Id);
        builder.Property(feature => feature.Id).ValueGeneratedNever();
        builder.Property(feature => feature.Code).HasMaxLength(60);
        builder.Property(feature => feature.Name).HasMaxLength(100);
        builder.Property(feature => feature.Kind).HasConversion<string>().HasMaxLength(20);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(feature => feature.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(feature => new { feature.TenantId, feature.Code }).IsUnique();
        builder.HasIndex(feature => new { feature.TenantId, feature.Name }).IsUnique();
    }
}