namespace EntryHub.Api.Domain.Webhooks;

/// <summary>
/// The events a webhook can ask for.
/// </summary>
public static class WebhookEvents
{
    public const string BookingCreated = "booking.created";
    public const string BookingCancelled = "booking.cancelled";
    public const string BookingRescheduled = "booking.rescheduled";

    public static readonly IReadOnlyList<string> All = [BookingCreated, BookingCancelled, BookingRescheduled];
}