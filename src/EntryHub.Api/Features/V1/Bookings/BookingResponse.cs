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
    DateTimeOffset? CancelledAt,
    DateTimeOffset? RescheduledAt = null,
    string? CreatedBy = null,
    DateTimeOffset? CheckedInAt = null,
    string? DecidedBy = null,
    DateTimeOffset? DecidedAt = null,
    string? DecisionReason = null)
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
            booking.CancelledAt,
            booking.RescheduledAt,
            booking.CreatedBy ?? booking.OrganizerId,
            booking.CheckedInAt,
            booking.DecidedBy,
            booking.DecidedAt,
            booking.DecisionReason);
}