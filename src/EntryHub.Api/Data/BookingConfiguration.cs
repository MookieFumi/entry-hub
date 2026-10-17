using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Domain.Rooms;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EntryHub.Api.Data;

internal sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("bookings", table => table.HasCheckConstraint("ck_bookings_end_after_start", "\"EndAt\" > \"StartAt\""));
        builder.HasKey(booking => booking.Id);
        builder.Property(booking => booking.Id).ValueGeneratedNever();
        builder.Property(booking => booking.OrganizerId).HasMaxLength(200);
        builder.Property(booking => booking.Title).HasMaxLength(120);
        builder.Property(booking => booking.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasOne<Room>().WithMany().HasForeignKey(booking => booking.RoomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(booking => new { booking.OrganizerId, booking.Status, booking.EndAt });

        // The rule that two confirmed bookings of one room cannot overlap is an exclusion constraint that EF Core
        // cannot model. It is created with SQL in the AddBookings migration (see that file).
    }
}