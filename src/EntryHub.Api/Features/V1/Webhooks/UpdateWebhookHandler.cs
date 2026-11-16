using EntryHub.Api.Data;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Features.V1.Webhooks;

public sealed class UpdateWebhookHandler(EntryHubDbContext db)
{
    /// <returns><see langword="null"/> when the subscription does not exist.</returns>
    public async Task<WebhookResponse?> HandleAsync(Guid id, UpdateWebhookRequest request, CancellationToken cancellationToken)
    {
        var webhook = await db.WebhookSubscriptions.SingleOrDefaultAsync(webhook => webhook.Id == id, cancellationToken);

        if (webhook is null)
        {
            return null;
        }

        webhook.IsEnabled = request.IsEnabled;
        await db.SaveChangesAsync(cancellationToken);

        return WebhookResponse.From(webhook);
    }
}