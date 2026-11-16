namespace EntryHub.Reminders.Notifications;

/// <summary>
/// What a reminder says. <paramref name="Email"/> is where it goes; the booking may have none.
/// </summary>
public sealed record ReminderMessage(Guid BookingId, string OrganizerId, string? Email, string Title, string RoomName, DateTimeOffset StartAt);

public enum NotificationOutcome
{
    /// <summary>
    /// Delivered, or handed to the channel. The reminder is done.
    /// </summary>
    Sent,

    /// <summary>
    /// There was nowhere to send it. The reminder is done: trying again would not help.
    /// </summary>
    NoRecipient,

    /// <summary>
    /// The channel failed. The reminder is not done and is tried again in a later round.
    /// </summary>
    Failed,
}

/// <summary>
/// How a reminder reaches the organizer of a booking.
/// </summary>
public interface INotificationSender
{
    Task<NotificationOutcome> SendReminderAsync(ReminderMessage message, CancellationToken cancellationToken);
}