using System.ComponentModel.DataAnnotations;

using EntryHub.Api.Domain.Webhooks;

namespace EntryHub.Api.Features.V1.Webhooks;

/// <param name="Url">Where to send the events: <c>https</c>, with a public host name or address.</param>
/// <param name="Events">What to be told about: <c>booking.created</c>, <c>booking.cancelled</c> or <c>booking.rescheduled</c>.</param>
public sealed record CreateWebhookRequest(
    [property: Required, StringLength(2048)] string Url,
    [property: Required, MinLength(1), MaxLength(3)] IReadOnlyList<string> Events);

/// <param name="IsEnabled">False pauses the subscription without deleting it.</param>
public sealed record UpdateWebhookRequest(bool IsEnabled);

/// <summary>
/// A webhook subscription. The signing secret is only in the response that creates it.
/// </summary>
/// <param name="Id">Identifier of the subscription.</param>
/// <param name="Url">Where the events are sent.</param>
/// <param name="Events">The events it receives.</param>
/// <param name="IsEnabled">Whether it is receiving events.</param>
/// <param name="CreatedAt">When it was created.</param>
public sealed record WebhookResponse(Guid Id, string Url, IReadOnlyList<string> Events, bool IsEnabled, DateTimeOffset CreatedAt)
{
    public static WebhookResponse From(WebhookSubscription webhook) =>
        new(webhook.Id, webhook.Url, webhook.Events, webhook.IsEnabled, webhook.CreatedAt);
}

/// <param name="Id">Identifier of the subscription.</param>
/// <param name="Url">Where the events are sent.</param>
/// <param name="Events">The events it receives.</param>
/// <param name="IsEnabled">Whether it is receiving events.</param>
/// <param name="CreatedAt">When it was created.</param>
/// <param name="Secret">Signs every delivery with HMAC-SHA256. Store it now: it is not shown again.</param>
public sealed record WebhookCreatedResponse(Guid Id, string Url, IReadOnlyList<string> Events, bool IsEnabled, DateTimeOffset CreatedAt, string Secret)
{
    public static WebhookCreatedResponse From(WebhookSubscription webhook) =>
        new(webhook.Id, webhook.Url, webhook.Events, webhook.IsEnabled, webhook.CreatedAt, webhook.Secret);
}