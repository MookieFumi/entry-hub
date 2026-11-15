using EntryHub.Api.Tenancy;

namespace EntryHub.Api.Domain.Bookings;

/// <summary>
/// Remembers which booking an <c>Idempotency-Key</c> created, so a retried request returns that booking
/// instead of creating a second one.
/// </summary>
public sealed class IdempotencyRecord : ITenantOwned
{
    public const int MaxKeyLength = 100;

    /// <summary>
    /// How long a key is remembered.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    public Guid Id { get; init; }

    public Guid TenantId { get; set; }

    /// <summary>
    /// The <c>sub</c> claim of the caller: keys are private to each user, so two users can pick the same key.
    /// </summary>
    public required string UserId { get; init; }

    public required string Key { get; init; }

    /// <summary>
    /// Hash of the request body, to tell a retry from a different request that reuses the key.
    /// </summary>
    public required string RequestHash { get; init; }

    public Guid BookingId { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
