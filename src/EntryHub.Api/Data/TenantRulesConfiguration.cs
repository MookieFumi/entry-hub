using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Domain.Tenants;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EntryHub.Api.Data;

internal sealed class TenantRulesConfiguration : IEntityTypeConfiguration<TenantRules>
{
    public void Configure(EntityTypeBuilder<TenantRules> builder)
    {
        builder.ToTable("tenant_rules");
        builder.HasKey(rules => rules.TenantId);
        builder.Property(rules => rules.TenantId).ValueGeneratedNever();
        builder.HasOne<Tenant>().WithOne().HasForeignKey<TenantRules>(rules => rules.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}