namespace EntryHub.Api.Domain.Bookings;

/// <summary>
/// A room reserved for a half-open time range: it includes <see cref="StartAt"/> and excludes <see cref="EndAt"/>.
/// </summary>
public sealed class Booking
{
    public Guid Id { get; init; }

    public Guid RoomId { get; init; }

    /// <summary>
    /// The <c>sub</c> claim of the person who booked. There is no users table: identity comes from the token.
    /// </summary>
    public required string OrganizerId { get; init; }

    public required string Title { get; init; }

    public DateTimeOffset StartAt { get; init; }

    public DateTimeOffset EndAt { get; init; }

    public int Attendees { get; init; }

    public BookingStatus Status { get; set; } = BookingStatus.Confirmed;

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? CancelledAt { get; set; }

    /// <summary>
    /// When the reminders service told the organizer that the booking is about to start; it is set once.
    /// </summary>
    public DateTimeOffset? ReminderSentAt { get; set; }
}