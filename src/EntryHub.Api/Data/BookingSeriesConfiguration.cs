using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Domain.Rooms;
using EntryHub.Api.Domain.Tenants;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EntryHub.Api.Data;

internal sealed class BookingSeriesConfiguration : IEntityTypeConfiguration<BookingSeries>
{
    public void Configure(EntityTypeBuilder<BookingSeries> builder)
    {
        builder.ToTable("booking_series");
        builder.HasKey(series => series.Id);
        builder.Property(series => series.Id).ValueGeneratedNever();
        builder.Property(series => series.OrganizerId).HasMaxLength(200);
        builder.Property(series => series.Frequency).HasConversion<string>().HasMaxLength(20);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(series => series.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Room>().WithMany().HasForeignKey(series => series.RoomId).OnDelete(DeleteBehavior.Restrict);
    }
}