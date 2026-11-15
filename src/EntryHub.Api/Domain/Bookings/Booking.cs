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

    /// <summary>
    /// The <c>sub</c> claim of who made the booking when it was not the organizer (Facilities booking on behalf of
    /// someone); <see langword="null"/> when the organizer booked for themselves.
    /// </summary>
    public string? CreatedBy { get; init; }

    public required string Title { get; init; }

    public DateTimeOffset StartAt { get; set; }

    public DateTimeOffset EndAt { get; set; }

    public int Attendees { get; init; }

    /// <summary>
    /// Buffer of the room before the booking, copied when the booking is made or moved so that changing the room
    /// later does not rewrite bookings that already exist.
    /// </summary>
    public int BufferBeforeMinutes { get; set; }

    /// <summary>
    /// Buffer of the room after the booking, copied like <see cref="BufferBeforeMinutes"/>.
    /// </summary>
    public int BufferAfterMinutes { get; set; }

    /// <summary>
    /// <see cref="StartAt"/> minus the buffer before; the database compares these two instants, not the visible times,
    /// so a room is never booked inside the buffer of another booking. Set when the booking is saved.
    /// </summary>
    public DateTimeOffset BlockedFrom { get; set; }

    /// <summary>
    /// <see cref="EndAt"/> plus the buffer after. Set when the booking is saved.
    /// </summary>
    public DateTimeOffset BlockedUntil { get; set; }

    /// <summary>
    /// When the booking was last moved to another time or room; <see langword="null"/> if it never was.
    /// </summary>
    public DateTimeOffset? RescheduledAt { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Confirmed;

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? CancelledAt { get; set; }

    /// <summary>
    /// When someone confirmed they showed up; <see langword="null"/> until then. Set once.
    /// </summary>
    public DateTimeOffset? CheckedInAt { get; set; }

    /// <summary>
    /// The <c>sub</c> claim of the Facilities user who approved or rejected a pending booking.
    /// </summary>
    public string? DecidedBy { get; set; }

    public DateTimeOffset? DecidedAt { get; set; }

    /// <summary>
    /// Why a pending booking was rejected, when whoever rejected it said so.
    /// </summary>
    public string? DecisionReason { get; set; }

    /// <summary>
    /// The series this booking is an occurrence of; <see langword="null"/> for a single booking.
    /// </summary>
    public Guid? SeriesId { get; set; }

    /// <summary>
    /// Place of the occurrence among the ones the series asked for, starting at 1. Skipped occurrences leave gaps.
    /// </summary>
    public int? SeriesPosition { get; set; }

    /// <summary>
    /// When the reminders service told the organizer that the booking is about to start; it is set once.
    /// </summary>
    public DateTimeOffset? ReminderSentAt { get; set; }
}