using EntryHub.Api.Data;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Features.V1.Webhooks;

public sealed class DeleteWebhookHandler(EntryHubDbContext db)
{
    /// <returns><see langword="false"/> when the subscription does not exist.</returns>
    public async Task<bool> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var webhook = await db.WebhookSubscriptions.SingleOrDefaultAsync(webhook => webhook.Id == id, cancellationToken);

        if (webhook is null)
        {
            return false;
        }

        db.WebhookSubscriptions.Remove(webhook);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}