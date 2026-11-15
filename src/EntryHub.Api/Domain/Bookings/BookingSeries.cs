using EntryHub.Api.Tenancy;

namespace EntryHub.Api.Domain.Bookings;

/// <summary>
/// The rule that produced a set of recurring bookings. The occurrences are ordinary bookings that point to it.
/// </summary>
public sealed class BookingSeries : ITenantOwned
{
    /// <summary>
    /// Most occurrences one series can ask for.
    /// </summary>
    public const int MaxOccurrences = 52;

    public Guid Id { get; init; }

    public Guid TenantId { get; set; }

    public Guid RoomId { get; init; }

    public required string OrganizerId { get; init; }

    public RecurrenceFrequency Frequency { get; init; }

    /// <summary>
    /// Every how many days or weeks the booking repeats.
    /// </summary>
    public int Interval { get; init; }

    /// <summary>
    /// How many occurrences were asked for; <see langword="null"/> when the series ends on <see cref="Until"/>.
    /// </summary>
    public int? Count { get; init; }

    /// <summary>
    /// Last day, in the time zone of the site, on which an occurrence may start.
    /// </summary>
    public DateOnly? Until { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}