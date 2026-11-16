using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Domain.Webhooks;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace EntryHub.Api.Data;

/// <summary>
/// Turns the booking changes of a save into webhook events. The deliveries are added to the same unit of work, so a
/// booking and the events about it are saved together or not at all.
/// </summary>
internal static class WebhookOutbox
{
    /// <summary>
    /// The events caused by the pending changes: a new booking, a booking that becomes cancelled and a booking that
    /// is moved.
    /// </summary>
    public static List<(string Type, Booking Booking)> Detect(IEnumerable<EntityEntry> entries)
    {
        var events = new List<(string, Booking)>();

        foreach (var entry in entries)
        {
            if (entry.Entity is not Booking booking)
            {
                continue;
            }

            if (entry.State == EntityState.Added)
            {
                events.Add((WebhookEvents.BookingCreated, booking));
            }
            else if (entry.State == EntityState.Modified)
            {
                if (entry.Property(nameof(Booking.Status)).IsModified && booking.Status == BookingStatus.Cancelled)
                {
                    events.Add((WebhookEvents.BookingCancelled, booking));
                }
                else if (entry.Property(nameof(Booking.RescheduledAt)).IsModified)
                {
                    events.Add((WebhookEvents.BookingRescheduled, booking));
                }
            }
        }

        return events;
    }

    public static IEnumerable<WebhookDelivery> Build(
        IEnumerable<(string Type, Booking Booking)> events,
        IReadOnlyList<WebhookSubscription> subscriptions,
        DateTimeOffset now)
    {
        foreach (var (type, booking) in events)
        {
            var wanted = subscriptions.Where(subscription => subscription.Events.Contains(type)).ToList();

            if (wanted.Count == 0)
            {
                continue;
            }

            var eventId = Guid.CreateVersion7();
            var payload = WebhookPayload.Serialize(new WebhookEnvelope(eventId, type, now, WebhookBooking.From(booking)));

            foreach (var subscription in wanted)
            {
                yield return new WebhookDelivery
                {
                    Id = Guid.CreateVersion7(),
                    SubscriptionId = subscription.Id,
                    EventId = eventId,
                    EventType = type,
                    Payload = payload,
                    NextAttemptAt = now,
                    CreatedAt = now,
                };
            }
        }
    }
}