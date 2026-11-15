using EntryHub.Api.Domain.Sites;
using EntryHub.Api.Domain.Tenants;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EntryHub.Api.Data;

internal sealed class SiteConfiguration : IEntityTypeConfiguration<Site>
{
    public void Configure(EntityTypeBuilder<Site> builder)
    {
        builder.ToTable("sites");
        builder.HasKey(site => site.Id);
        builder.Property(site => site.Id).ValueGeneratedNever();
        builder.Property(site => site.Name).HasMaxLength(100);
        builder.Property(site => site.TimeZoneId).HasMaxLength(64);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(site => site.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(site => new { site.TenantId, site.Name }).IsUnique();
    }
}