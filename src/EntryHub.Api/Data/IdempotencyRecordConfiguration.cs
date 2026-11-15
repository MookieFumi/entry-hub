using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Domain.Tenants;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EntryHub.Api.Data;

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("idempotency_records");
        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id).ValueGeneratedNever();
        builder.Property(record => record.UserId).HasMaxLength(200);
        builder.Property(record => record.Key).HasMaxLength(IdempotencyRecord.MaxKeyLength);
        builder.Property(record => record.RequestHash).HasMaxLength(64);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(record => record.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Booking>().WithMany().HasForeignKey(record => record.BookingId).OnDelete(DeleteBehavior.Cascade);

        // The unique index is what makes two simultaneous requests with the same key safe: one insert wins.
        builder.HasIndex(record => new { record.TenantId, record.UserId, record.Key }).IsUnique();
        builder.HasIndex(record => record.CreatedAt);
    }
}
