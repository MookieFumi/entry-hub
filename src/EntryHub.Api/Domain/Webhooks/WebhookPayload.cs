using System.Text.Json;
using System.Text.Json.Serialization;

using EntryHub.Api.Domain.Bookings;

namespace EntryHub.Api.Domain.Webhooks;

/// <summary>
/// The body of a webhook request: the event and the booking as it is after the change.
/// </summary>
/// <param name="Id">Identifier of the event; a receiver can use it to ignore a repeated delivery.</param>
/// <param name="Type">One of <see cref="WebhookEvents"/>.</param>
/// <param name="CreatedAt">When the change happened.</param>
/// <param name="Data">The booking.</param>
public sealed record WebhookEnvelope(Guid Id, string Type, DateTimeOffset CreatedAt, WebhookBooking Data);

public sealed record WebhookBooking(
    Guid Id,
    Guid RoomId,
    string OrganizerId,
    string Title,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    int Attendees,
    string Status,
    Guid? SeriesId,
    int? SeriesPosition)
{
    public static WebhookBooking From(Booking booking) =>
        new(
            booking.Id,
            booking.RoomId,
            booking.OrganizerId,
            booking.Title,
            booking.StartAt,
            booking.EndAt,
            booking.Attendees,
            booking.Status.ToString(),
            booking.SeriesId,
            booking.SeriesPosition);
}

public static class WebhookPayload
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.Never };

    public static string Serialize(WebhookEnvelope envelope) => JsonSerializer.Serialize(envelope, Options);
}