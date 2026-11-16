using EntryHub.Api.Data;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Features.V1.Webhooks;

public sealed class ListWebhooksHandler(EntryHubDbContext db)
{
    public async Task<IReadOnlyList<WebhookResponse>> HandleAsync(CancellationToken cancellationToken)
    {
        var webhooks = await db.WebhookSubscriptions.AsNoTracking().OrderBy(webhook => webhook.CreatedAt).ThenBy(webhook => webhook.Id).ToListAsync(cancellationToken);

        return [.. webhooks.Select(WebhookResponse.From)];
    }
}