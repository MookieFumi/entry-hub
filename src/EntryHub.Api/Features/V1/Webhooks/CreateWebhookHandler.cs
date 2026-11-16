using System.Security.Cryptography;

using EntryHub.Api.Data;
using EntryHub.Api.Domain.Webhooks;
using EntryHub.Api.Features.V1.Common;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Features.V1.Webhooks;

public abstract record CreateWebhookResult
{
    private CreateWebhookResult()
    {
    }

    public sealed record Created(WebhookCreatedResponse Webhook) : CreateWebhookResult;

    public sealed record Invalid(string Message) : CreateWebhookResult;

    public sealed record LimitReached : CreateWebhookResult;
}

public sealed class CreateWebhookHandler(EntryHubDbContext db, TimeProvider clock)
{
    public async Task<CreateWebhookResult> HandleAsync(Caller caller, CreateWebhookRequest request, CancellationToken cancellationToken)
    {
        var url = request.Url.Trim();

        if (WebhookUrlGuard.Validate(url) is { } problem)
        {
            return new CreateWebhookResult.Invalid(problem);
        }

        var events = request.Events.Select(name => name.Trim()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        if (events.FirstOrDefault(name => !WebhookEvents.All.Contains(name)) is { } unknown)
        {
            return new CreateWebhookResult.Invalid($"Unknown event '{unknown}'. Use {string.Join(", ", WebhookEvents.All)}.");
        }

        if (await db.WebhookSubscriptions.CountAsync(cancellationToken) >= WebhookSubscription.MaxPerTenant)
        {
            return new CreateWebhookResult.LimitReached();
        }

        var webhook = new WebhookSubscription
        {
            Id = Guid.CreateVersion7(),
            Url = url,
            Events = events,
            Secret = "whsec_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_'),
            CreatedBy = caller.Id,
            CreatedAt = clock.GetUtcNow(),
        };

        db.WebhookSubscriptions.Add(webhook);
        await db.SaveChangesAsync(cancellationToken);

        return new CreateWebhookResult.Created(WebhookCreatedResponse.From(webhook));
    }
}