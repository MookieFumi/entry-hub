using EntryHub.Reminders.Notifications;

using Microsoft.Extensions.Options;

namespace EntryHub.Reminders;

/// <summary>
/// Looks for bookings that are about to start and reminds their organizers, and releases the ones nobody checked in to
/// when their tenant asked for it. How a reminder reaches the organizer is up to the <see cref="INotificationSender"/>:
/// a log entry by default, an email when the SMTP settings are on.
/// </summary>
public sealed partial class ReminderWorker(
    ReminderStore store,
    INotificationSender sender,
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
        var due = await store.ClaimDueAsync(clock.GetUtcNow(), leadTime, options.Value.RetryAfter, cancellationToken);
        var done = new List<DueReminder>();
        var (sent, withoutRecipient, failed) = (0, 0, 0);

        foreach (var reminder in due)
        {
            var outcome = await sender.SendReminderAsync(
                new ReminderMessage(reminder.BookingId, reminder.OrganizerId, reminder.Email, reminder.Title, reminder.RoomName, reminder.StartAt),
                cancellationToken);

            switch (outcome)
            {
                case NotificationOutcome.Sent:
                    sent++;
                    done.Add(reminder);
                    break;
                case NotificationOutcome.NoRecipient:
                    withoutRecipient++;
                    done.Add(reminder);
                    break;
                default:
                    failed++;
                    break;
            }
        }

        if (done.Count > 0)
        {
            await store.MarkRemindedAsync(done, clock.GetUtcNow(), cancellationToken);
        }

        if (due.Count > 0)
        {
            LogRound(sent, withoutRecipient, failed);
        }

        foreach (var bookingId in await store.ReleaseUncheckedAsync(clock.GetUtcNow(), cancellationToken))
        {
            LogReleased(bookingId);
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Reminders: {Sent} sent, {WithoutRecipient} without an address, {Failed} to try again")]
    private partial void LogRound(int sent, int withoutRecipient, int failed);

    [LoggerMessage(Level = LogLevel.Information, Message = "Booking {BookingId} released: nobody checked in")]
    private partial void LogReleased(Guid bookingId);

    [LoggerMessage(Level = LogLevel.Error, Message = "The reminder round failed; the next one will try again")]
    private partial void LogRoundFailed(Exception exception);
}