using System.Net;
using System.Net.Mail;

using Microsoft.Extensions.Options;

namespace EntryHub.Reminders.Notifications;

/// <summary>
/// Sends each reminder as a plain text email through the configured SMTP server. A booking without an address is not
/// an error: it has nowhere to go. The log only says that a send failed and why (the type of error), never to whom.
/// </summary>
public sealed partial class SmtpNotificationSender(IOptions<NotificationOptions> options, ILogger<SmtpNotificationSender> logger) : INotificationSender
{
    public async Task<NotificationOutcome> SendReminderAsync(ReminderMessage message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message.Email))
        {
            return NotificationOutcome.NoRecipient;
        }

        var settings = options.Value.Smtp;

        try
        {
            using var client = new SmtpClient(settings.Host, settings.Port)
            {
                EnableSsl = settings.UseSsl,
                Timeout = (int)settings.Timeout.TotalMilliseconds,
                Credentials = string.IsNullOrEmpty(settings.UserName) ? null : new NetworkCredential(settings.UserName, settings.Password),
            };

            using var mail = new MailMessage(settings.From!, message.Email)
            {
                Subject = $"Reminder: {SingleLine(message.Title)} starts soon",
                Body = $"Your booking \"{SingleLine(message.Title)}\" in {SingleLine(message.RoomName)} starts at {message.StartAt:yyyy-MM-dd HH:mm} UTC.",
            };

            await client.SendMailAsync(mail, cancellationToken);
            return NotificationOutcome.Sent;
        }
        catch (Exception exception) when (exception is SmtpException or InvalidOperationException or FormatException or ArgumentException or IOException)
        {
            LogSendFailed(message.BookingId, exception.GetType().Name);
            return NotificationOutcome.Failed;
        }
    }

    // A title with a line break must not be able to add headers to the mail.
    private static string SingleLine(string text) => text.ReplaceLineEndings(" ");

    [LoggerMessage(Level = LogLevel.Warning, Message = "The reminder for booking {BookingId} could not be sent ({ErrorType}); the next round will try again")]
    private partial void LogSendFailed(Guid bookingId, string errorType);
}