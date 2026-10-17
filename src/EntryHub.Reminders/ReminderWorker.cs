using Microsoft.Extensions.Options;

namespace EntryHub.Reminders;

/// <summary>
/// Looks for bookings that are about to start and reminds their organizers. The reminder is a structured log entry,
/// which the Aspire dashboard shows; sending an email or a push notification would replace the log line.
/// </summary>
public sealed partial class ReminderWorker(
    ReminderStore store,
    IOptions<ReminderOptions> options,
    TimeProvider clock,
    ILogger<ReminderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        using var timer = new PeriodicTimer(settings.PollInterval, clock);

        do
        {
            try
            {
                await RemindAsync(settings.LeadTime, stoppingToken);
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
    public async Task RemindAsync(TimeSpan leadTime, CancellationToken cancellationToken)
    {
        foreach (var reminder in await store.ClaimDueAsync(clock.GetUtcNow(), leadTime, cancellationToken))
        {
            LogReminder(reminder.OrganizerId, reminder.Title, reminder.RoomName, reminder.StartAt);
        }
    }

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

    [LoggerMessage(Level = LogLevel.Information, Message = "Reminder for {OrganizerId}: \"{Title}\" in {RoomName} starts at {StartAt:u}")]
    private partial void LogReminder(string organizerId, string title, string roomName, DateTimeOffset startAt);

    [LoggerMessage(Level = LogLevel.Error, Message = "The reminder round failed; the next one will try again")]
    private partial void LogRoundFailed(Exception exception);
}