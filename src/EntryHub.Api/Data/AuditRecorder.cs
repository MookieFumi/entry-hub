using EntryHub.Api.Domain.Audit;
using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Domain.Features;
using EntryHub.Api.Domain.Rooms;
using EntryHub.Api.Domain.Sites;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace EntryHub.Api.Data;

/// <summary>
/// Turns the changes pending in a context into audit entries, so no handler has to remember to write one.
/// </summary>
internal static class AuditRecorder
{
    /// <summary>
    /// The entries for what is about to be saved. Call it before saving, while the states are still known.
    /// </summary>
    public static List<AuditEntry> Record(IEnumerable<EntityEntry> entries, string actorId, Guid tenantId, DateTimeOffset now)
    {
        var audit = new List<AuditEntry>();

        foreach (var entry in entries)
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted) || Describe(entry.Entity) is not { } what)
            {
                continue;
            }

            var changed = entry.State == EntityState.Modified
                ? entry.Properties.Where(property => property.IsModified && !Equals(property.OriginalValue, property.CurrentValue)).Select(property => property.Metadata.Name).ToList()
                : [];

            if (entry.State == EntityState.Modified && changed.Count == 0)
            {
                continue;
            }

            // The reminders service marks a booking as reminded: that is the system, not a change worth a line.
            if (entry.Entity is Booking && changed is [nameof(Booking.ReminderSentAt)])
            {
                continue;
            }

            audit.Add(new AuditEntry
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenantId,
                At = now,
                ActorId = actorId,
                Action = ActionOf(entry, changed),
                EntityType = what.Type,
                EntityId = what.Id,
                Summary = Summarize(entry.State, what.Name, changed),
            });
        }

        return audit;
    }

    private static string ActionOf(EntityEntry entry, List<string> changed) => entry.State switch
    {
        EntityState.Added => "Created",
        EntityState.Deleted => "Deleted",
        _ when entry.Entity is Booking booking && changed.Contains(nameof(Booking.Status)) && booking.Status == BookingStatus.Cancelled => "Cancelled",
        _ when entry.Entity is Booking && changed.Contains(nameof(Booking.RescheduledAt)) => "Rescheduled",
        _ => "Updated",
    };

    private static string? Summarize(EntityState state, string? name, List<string> changed)
    {
        var summary = state == EntityState.Modified ? "Changed: " + string.Join(", ", changed) : name;

        return summary is { Length: > 500 } ? summary[..500] : summary;
    }

    private static (string Type, Guid Id, string? Name)? Describe(object entity) => entity switch
    {
        Booking booking => ("Booking", booking.Id, null),
        Room room => ("Room", room.Id, room.Name),
        Site site => ("Site", site.Id, site.Name),
        TenantRules rules => ("Rules", rules.TenantId, null),
        Feature feature => ("Feature", feature.Id, feature.Name),
        SiteHoliday holiday => ("SiteHoliday", holiday.Id, holiday.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)),
        RoomBlock block => ("RoomBlock", block.Id, block.Reason),
        _ => null,
    };
}