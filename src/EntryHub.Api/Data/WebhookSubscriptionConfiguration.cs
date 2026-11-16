using EntryHub.Api.Domain.Tenants;
using EntryHub.Api.Domain.Webhooks;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EntryHub.Api.Data;

internal sealed class WebhookSubscriptionConfiguration : IEntityTypeConfiguration<WebhookSubscription>
{
    public void Configure(EntityTypeBuilder<WebhookSubscription> builder)
    {
        builder.ToTable("webhook_subscriptions");
        builder.HasKey(webhook => webhook.Id);
        builder.Property(webhook => webhook.Id).ValueGeneratedNever();
        builder.Property(webhook => webhook.Url).HasMaxLength(2048);
        builder.Property(webhook => webhook.Secret).HasMaxLength(100);
        builder.Property(webhook => webhook.CreatedBy).HasMaxLength(200);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(webhook => webhook.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(webhook => webhook.TenantId);
    }
}