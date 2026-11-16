namespace EntryHub.Reminders.Notifications;

/// <summary>
/// Settings under the <c>Notifications</c> configuration section. Without them reminders are only logged.
/// </summary>
public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    public SmtpOptions Smtp { get; set; } = new();
}

/// <summary>
/// The mail server for reminders, under <c>Notifications:Smtp</c>. Off until <see cref="Enabled"/> is true.
/// </summary>
public sealed class SmtpOptions
{
    public bool Enabled { get; set; }

    public string? Host { get; set; }

    public int Port { get; set; } = 587;

    /// <summary>
    /// Encrypt the connection (STARTTLS on port 587, TLS on 465). Keep it on unless the server is local.
    /// </summary>
    public bool UseSsl { get; set; } = true;

    public string? UserName { get; set; }

    public string? Password { get; set; }

    /// <summary>
    /// The sender address of every reminder.
    /// </summary>
    public string? From { get; set; }

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// True when the settings are enough to send: it is on and has a host and a sender.
    /// </summary>
    public bool IsUsable => Enabled && !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(From);
}