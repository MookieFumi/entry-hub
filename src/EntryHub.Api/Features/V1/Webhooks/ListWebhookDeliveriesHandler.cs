using EntryHub.Api.Data;
using EntryHub.Api.Domain.Webhooks;
using EntryHub.Api.Features.V1.Common;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Features.V1.Webhooks;

/// <param name="Status">Only deliveries in this state (<c>Pending</c>, <c>Succeeded</c> or <c>Failed</c>).</param>
/// <param name="Page">Page number, from 1.</param>
/// <param name="PageSize">Items per page.</param>
public sealed record ListWebhookDeliveriesQuery(WebhookDeliveryStatus? Status, int? Page, int? PageSize);

/// <summary>
/// What happened when an event was sent to a subscription. The body is not included.
/// </summary>
/// <param name="Id">Identifier of the delivery.</param>
/// <param name="EventId">Identifier of the event, also in the body that was sent.</param>
/// <param name="EventType">The event, such as <c>booking.created</c>.</param>
/// <param name="Status">Pending while attempts remain, then Succeeded or Failed.</param>
/// <param name="Attempts">How many times it was sent.</param>
/// <param name="NextAttemptAt">When the next attempt is due, while it is pending.</param>
/// <param name="LastAttemptAt">When it was last sent.</param>
/// <param name="LastStatusCode">HTTP status of the last attempt, when the receiver answered.</param>
/// <param name="LastError">Why the last attempt failed.</param>
/// <param name="CreatedAt">When the event happened.</param>
/// <param name="CompletedAt">When it succeeded or was given up.</param>
public sealed record WebhookDeliveryResponse(
    Guid Id,
    Guid EventId,
    string EventType,
    WebhookDeliveryStatus Status,
    int Attempts,
    DateTimeOffset? NextAttemptAt,
    DateTimeOffset? LastAttemptAt,
    int? LastStatusCode,
    string? LastError,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt)
{
    public static WebhookDeliveryResponse From(WebhookDelivery delivery) =>
        new(
            delivery.Id,
            delivery.EventId,
            delivery.EventType,
            delivery.Status,
            delivery.Attempts,
            delivery.Status == WebhookDeliveryStatus.Pending ? delivery.NextAttemptAt : null,
            delivery.LastAttemptAt,
            delivery.LastStatusCode,
            delivery.LastError,
            delivery.CreatedAt,
            delivery.CompletedAt);
}

public sealed class ListWebhookDeliveriesHandler(EntryHubDbContext db)
{
    /// <returns><see langword="null"/> when the subscription does not exist.</returns>
    public async Task<PagedResponse<WebhookDeliveryResponse>?> HandleAsync(Guid id, ListWebhookDeliveriesQuery query, CancellationToken cancellationToken)
    {
        if (!await db.WebhookSubscriptions.AnyAsync(webhook => webhook.Id == id, cancellationToken))
        {
            return null;
        }

        var paging = Paging.Normalize(query.Page, query.PageSize);
        var deliveries = db.WebhookDeliveries.AsNoTracking().Where(delivery => delivery.SubscriptionId == id);

        if (query.Status is { } status)
        {
            deliveries = deliveries.Where(delivery => delivery.Status == status);
        }

        var total = await deliveries.CountAsync(cancellationToken);
        var page = await deliveries
            .OrderByDescending(delivery => delivery.CreatedAt).ThenByDescending(delivery => delivery.Id)
            .Skip(paging.Skip).Take(paging.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<WebhookDeliveryResponse>([.. page.Select(WebhookDeliveryResponse.From)], paging.Page, paging.PageSize, total);
    }
}