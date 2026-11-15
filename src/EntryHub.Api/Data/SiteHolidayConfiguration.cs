using EntryHub.Api.Domain.Sites;
using EntryHub.Api.Domain.Tenants;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EntryHub.Api.Data;

internal sealed class SiteHolidayConfiguration : IEntityTypeConfiguration<SiteHoliday>
{
    public void Configure(EntityTypeBuilder<SiteHoliday> builder)
    {
        builder.ToTable("site_holidays");
        builder.HasKey(holiday => holiday.Id);
        builder.Property(holiday => holiday.Id).ValueGeneratedNever();
        builder.Property(holiday => holiday.Name).HasMaxLength(100);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(holiday => holiday.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Site>().WithMany().HasForeignKey(holiday => holiday.SiteId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(holiday => new { holiday.SiteId, holiday.Date }).IsUnique();
    }
}