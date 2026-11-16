using EntryHub.Api.Tenancy;

namespace EntryHub.Api.Domain.Webhooks;

/// <summary>
/// A URL that receives a signed HTTP request when something happens to a booking of the tenant.
/// </summary>
public sealed class WebhookSubscription : ITenantOwned
{
    /// <summary>
    /// Most subscriptions a tenant can have.
    /// </summary>
    public const int MaxPerTenant = 10;

    public Guid Id { get; init; }

    public Guid TenantId { get; set; }

    /// <summary>
    /// Where the events are sent: an <c>https</c> URL that does not point inside a private network.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// The event names the subscription wants, see <see cref="WebhookEvents"/>.
    /// </summary>
    public required List<string> Events { get; init; }

    /// <summary>
    /// The key that signs each delivery (HMAC-SHA256). It is shown once, when the subscription is created.
    /// </summary>
    public required string Secret { get; init; }

    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// The <c>sub</c> claim of the Facilities user who created it.
    /// </summary>
    public required string CreatedBy { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}