using Microsoft.Extensions.Options;

namespace EntryHub.Reminders.Webhooks;

/// <summary>
/// Sends the webhook events that the API saved with each booking change. A failed delivery is tried again after
/// 30 seconds, then 1, 2, 4 and 8 minutes (the delay doubles), and is given up after six attempts. The result of each
/// attempt is kept in the delivery, which Facilities can read in the API.
/// </summary>
public sealed partial class WebhookWorker(
    WebhookDeliveryStore store,
    WebhookSender sender,
    IOptions<WebhookOptions> options,
    TimeProvider clock,
    ILogger<WebhookWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.PollInterval, clock);

        do
        {
            try
            {
                await DeliverAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A failed round (database restarting, for example) must not stop the service: the next tick retries.
                LogRoundFailed(exception);
            }
        }
        while (await WaitForNextTickAsync(timer, stoppingToken));
    }

    /// <summary>
    /// Runs one round; public so tests can drive it without waiting for the timer.
    /// </summary>
    public async Task DeliverAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var due = await store.ClaimDueAsync(clock.GetUtcNow(), settings.BatchSize, settings.Lease, cancellationToken);

        await Parallel.ForEachAsync(
            due,
            new ParallelOptions { MaxDegreeOfParallelism = Math.Max(settings.Concurrency, 1), CancellationToken = cancellationToken },
            async (item, token) =>
            {
                var (tenant, delivery) = item;
                var result = await sender.SendAsync(delivery, token);
                var attempt = delivery.Attempts + 1;
                var finalAttempt = !result.Succeeded && attempt >= settings.MaxAttempts;

                await store.RecordAsync(tenant, delivery.Id, clock.GetUtcNow(), result, finalAttempt, RetryDelay(settings, attempt), token);

                if (result.Succeeded)
                {
                    LogDelivered(delivery.Id, delivery.EventType, attempt);
                }
                else
                {
                    LogFailed(delivery.Id, delivery.EventType, attempt, result.Error, finalAttempt);
                }
            });
    }

    /// <summary>
    /// Wait after the given failed attempt: the base delay, doubled for each earlier failure.
    /// </summary>
    public static TimeSpan RetryDelay(WebhookOptions settings, int failedAttempt) =>
        settings.BaseDelay * Math.Pow(2, Math.Max(failedAttempt - 1, 0));

    private static async Task<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Webhook delivery {DeliveryId} ({EventType}) delivered on attempt {Attempt}")]
    private partial void LogDelivered(Guid deliveryId, string eventType, int attempt);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Webhook delivery {DeliveryId} ({EventType}) failed on attempt {Attempt}: {Error}. Given up: {GivenUp}")]
    private partial void LogFailed(Guid deliveryId, string eventType, int attempt, string? error, bool givenUp);

    [LoggerMessage(Level = LogLevel.Error, Message = "The webhook round failed; the next one will try again")]
    private partial void LogRoundFailed(Exception exception);
}