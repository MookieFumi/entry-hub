namespace EntryHub.Reminders.Notifications;

/// <summary>
/// The default: the reminder is a structured log entry, which the Aspire dashboard shows. It names the booking and
/// the time, never the title, the room or a person.
/// </summary>
public sealed partial class LogNotificationSender(ILogger<LogNotificationSender> logger) : INotificationSender
{
    public Task<NotificationOutcome> SendReminderAsync(ReminderMessage message, CancellationToken cancellationToken)
    {
        LogReminder(message.BookingId, message.StartAt);
        return Task.FromResult(NotificationOutcome.Sent);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Reminder for booking {BookingId}, which starts at {StartAt:u}")]
    private partial void LogReminder(Guid bookingId, DateTimeOffset startAt);
}