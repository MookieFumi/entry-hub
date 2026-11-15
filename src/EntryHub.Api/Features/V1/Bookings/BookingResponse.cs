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
    string? DecisionReason = null,
    Guid? SeriesId = null,
    int? SeriesPosition = null,
    SeriesOutcome? Series = null)
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
            booking.DecisionReason,
            booking.SeriesId,
            booking.SeriesPosition);
}

/// <summary>
/// What a recurring request produced: one entry for each occurrence it asked for. Only the response that creates the
/// series has it.
/// </summary>
/// <param name="Id">Identifier of the series, also in <c>seriesId</c> of each created booking.</param>
/// <param name="Requested">How many occurrences the recurrence asked for.</param>
/// <param name="Created">How many of them were booked.</param>
/// <param name="Occurrences">Every requested occurrence, booked or skipped.</param>
public sealed record SeriesOutcome(Guid Id, int Requested, int Created, IReadOnlyList<SeriesOccurrence> Occurrences);

/// <summary>
/// One occurrence of a series: booked when <paramref name="BookingId"/> is set, otherwise skipped for the rules in
/// <paramref name="Violations"/>.
/// </summary>
public sealed record SeriesOccurrence(
    int Position,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    Guid? BookingId,
    IReadOnlyList<RuleViolation> Violations);