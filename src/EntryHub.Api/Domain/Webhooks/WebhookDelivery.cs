using EntryHub.Api.Tenancy;

namespace EntryHub.Api.Domain.Webhooks;

public enum WebhookDeliveryStatus
{
    /// <summary>
    /// Waiting for its first attempt or for the next one.
    /// </summary>
    Pending,

    Succeeded,

    /// <summary>
    /// Given up: the attempts ran out, or the subscription was disabled.
    /// </summary>
    Failed,
}

/// <summary>
/// One event to send to one subscription. It is written in the same transaction as the booking change that caused it
/// (an outbox), and the reminders service delivers it and keeps the outcome here.
/// </summary>
public sealed class WebhookDelivery : ITenantOwned
{
    public Guid Id { get; init; }

    public Guid TenantId { get; set; }

    public Guid SubscriptionId { get; init; }

    /// <summary>
    /// Identifier of the event, the same for every subscription that receives it.
    /// </summary>
    public Guid EventId { get; init; }

    public required string EventType { get; init; }

    /// <summary>
    /// The JSON body that is sent, kept as text so that the signature always matches the bytes of the request.
    /// </summary>
    public required string Payload { get; init; }

    public WebhookDeliveryStatus Status { get; set; } = WebhookDeliveryStatus.Pending;

    public int Attempts { get; set; }

    /// <summary>
    /// When the next attempt is due while <see cref="Status"/> is pending.
    /// </summary>
    public DateTimeOffset NextAttemptAt { get; set; }

    public DateTimeOffset? LastAttemptAt { get; set; }

    /// <summary>
    /// The HTTP status of the last attempt; <see langword="null"/> when it did not get an answer.
    /// </summary>
    public int? LastStatusCode { get; set; }

    public string? LastError { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; set; }
}