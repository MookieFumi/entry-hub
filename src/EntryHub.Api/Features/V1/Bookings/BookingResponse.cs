using EntryHub.Api.Domain.Bookings;

namespace EntryHub.Api.Features.V1.Bookings;

public sealed record BookingResponse(
    Guid Id,
    Guid RoomId,
    string OrganizerId,
    string Title,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    int Attendees,
    BookingStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CancelledAt)
{
    public static BookingResponse From(Booking booking) =>
        new(
            booking.Id,
            booking.RoomId,
            booking.OrganizerId,
            booking.Title,
            booking.StartAt,
            booking.EndAt,
            booking.Attendees,
            booking.Status,
            booking.CreatedAt,
            booking.CancelledAt);
}