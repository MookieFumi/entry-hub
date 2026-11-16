namespace EntryHub.Reminders.Webhooks;

/// <summary>
/// Settings under the <c>Webhooks</c> configuration section.
/// </summary>
public sealed class WebhookOptions
{
    public const string SectionName = "Webhooks";

    /// <summary>
    /// How often the service looks for events to send.
    /// </summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Most deliveries one tenant gets in a round.
    /// </summary>
    public int BatchSize { get; set; } = 20;

    /// <summary>
    /// How many requests are in flight at once.
    /// </summary>
    public int Concurrency { get; set; } = 4;

    /// <summary>
    /// How many times a delivery is tried before it is given up.
    /// </summary>
    public int MaxAttempts { get; set; } = 6;

    /// <summary>
    /// Wait before the second attempt; it doubles with each failure.
    /// </summary>
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long a request may take, connecting included.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How long a delivery that was picked up stays reserved. If the service stops before it records the outcome, the
    /// delivery is tried again when the lease ends.
    /// </summary>
    public TimeSpan Lease { get; set; } = TimeSpan.FromMinutes(2);
}