using System.Security.Claims;

using EntryHub.Api.Features.V1.Common;
using EntryHub.Api.Security;

using Microsoft.AspNetCore.Http.HttpResults;

namespace EntryHub.Api.Features.V1.Webhooks;

public static class WebhooksEndpoints
{
    public static void MapWebhooksEndpoints(this RouteGroupBuilder v1)
    {
        var webhooks = v1.MapGroup("/webhooks").WithTags("Webhooks").RequireAuthorization(Policies.Facilities);

        webhooks.MapGet("/", ListAsync).WithName("ListWebhooks")
            .WithSummary("Lists the webhook subscriptions of the tenant (without their secrets). Facilities only.");

        webhooks.MapPost("/", CreateAsync).WithName("CreateWebhook")
            .WithSummary("Subscribes an https URL to booking events. The response shows the signing secret once. Facilities only.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        webhooks.MapPatch("/{id:guid}", UpdateAsync).WithName("UpdateWebhook")
            .WithSummary("Enables or disables a subscription. Facilities only.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        webhooks.MapGet("/{id:guid}/deliveries", ListDeliveriesAsync).WithName("ListWebhookDeliveries")
            .WithSummary("Lists what happened when events were sent to a subscription, newest first. Facilities only.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        webhooks.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteWebhook")
            .WithSummary("Deletes a subscription. Facilities only.")
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<Ok<IReadOnlyList<WebhookResponse>>> ListAsync(ListWebhooksHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(cancellationToken));

    private static async Task<Results<Created<WebhookCreatedResponse>, ValidationProblem, ProblemHttpResult>> CreateAsync(
        CreateWebhookRequest request,
        ClaimsPrincipal user,
        CreateWebhookHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(Caller.From(user), request, cancellationToken);

        return result switch
        {
            CreateWebhookResult.Created created => TypedResults.Created($"/api/v1/webhooks/{created.Webhook.Id}", created.Webhook),
            CreateWebhookResult.Invalid invalid => TypedResults.ValidationProblem(
                new Dictionary<string, string[]> { [nameof(request.Url)] = [invalid.Message] }),
            CreateWebhookResult.LimitReached => TypedResults.Problem(
                title: "Too many webhooks",
                detail: "The tenant already has the maximum number of webhook subscriptions. Delete one first.",
                statusCode: StatusCodes.Status409Conflict),
            _ => throw new InvalidOperationException($"Unhandled result {result.GetType().Name}."),
        };
    }

    private static async Task<Results<Ok<WebhookResponse>, ProblemHttpResult>> UpdateAsync(
        Guid id,
        UpdateWebhookRequest request,
        UpdateWebhookHandler handler,
        CancellationToken cancellationToken) =>
        await handler.HandleAsync(id, request, cancellationToken) is { } webhook
            ? TypedResults.Ok(webhook)
            : TypedResults.Problem(title: "Webhook not found", statusCode: StatusCodes.Status404NotFound);

    private static async Task<Results<Ok<PagedResponse<WebhookDeliveryResponse>>, ProblemHttpResult>> ListDeliveriesAsync(
        Guid id,
        [AsParameters] ListWebhookDeliveriesQuery query,
        ListWebhookDeliveriesHandler handler,
        CancellationToken cancellationToken) =>
        await handler.HandleAsync(id, query, cancellationToken) is { } page
            ? TypedResults.Ok(page)
            : TypedResults.Problem(title: "Webhook not found", statusCode: StatusCodes.Status404NotFound);

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteAsync(
        Guid id,
        DeleteWebhookHandler handler,
        CancellationToken cancellationToken) =>
        await handler.HandleAsync(id, cancellationToken)
            ? TypedResults.NoContent()
            : TypedResults.Problem(title: "Webhook not found", statusCode: StatusCodes.Status404NotFound);
}