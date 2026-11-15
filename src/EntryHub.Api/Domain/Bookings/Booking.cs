using EntryHub.Api.Tenancy;

namespace EntryHub.Api.Domain.Bookings;

/// <summary>
/// A room reserved for a half-open time range: it includes <see cref="StartAt"/> and excludes <see cref="EndAt"/>.
/// </summary>
public sealed class Booking : ITenantOwned
{
    public Guid Id { get; init; }

    public Guid TenantId { get; set; }

    public Guid RoomId { get; set; }

    /// <summary>
    /// The <c>sub</c> claim of the person who booked. There is no users table: identity comes from the token.
    /// </summary>
    public required string OrganizerId { get; init; }

    public required string Title { get; init; }

    public DateTimeOffset StartAt { get; set; }

    public DateTimeOffset EndAt { get; set; }

    public int Attendees { get; init; }

    /// <summary>
    /// When the booking was last moved to another time or room; <see langword="null"/> if it never was.
    /// </summary>
    public DateTimeOffset? RescheduledAt { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Confirmed;

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? CancelledAt { get; set; }

    /// <summary>
    /// When the reminders service told the organizer that the booking is about to start; it is set once.
    /// </summary>
    public DateTimeOffset? ReminderSentAt { get; set; }
}