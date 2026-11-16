using EntryHub.Api.Domain.Tenants;
using EntryHub.Api.Domain.Webhooks;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EntryHub.Api.Data;

internal sealed class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> builder)
    {
        builder.ToTable("webhook_deliveries");
        builder.HasKey(delivery => delivery.Id);
        builder.Property(delivery => delivery.Id).ValueGeneratedNever();
        builder.Property(delivery => delivery.EventType).HasMaxLength(50);
        builder.Property(delivery => delivery.Payload).HasColumnType("text");
        builder.Property(delivery => delivery.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(delivery => delivery.LastError).HasMaxLength(500);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(delivery => delivery.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WebhookSubscription>().WithMany().HasForeignKey(delivery => delivery.SubscriptionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(delivery => new { delivery.SubscriptionId, delivery.CreatedAt });
        builder.HasIndex(delivery => new { delivery.Status, delivery.NextAttemptAt });
    }
}