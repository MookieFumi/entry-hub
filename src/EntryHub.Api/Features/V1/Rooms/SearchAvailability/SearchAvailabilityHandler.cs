using EntryHub.Api.Data;
using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Features.V1.Sites;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Features.V1.Rooms.SearchAvailability;

public sealed class SearchAvailabilityHandler(EntryHubDbContext db)
{
    /// <summary>
    /// Active rooms with no confirmed booking that overlaps the slot once the buffers of the room and of that booking are counted, in sites that are open that day. It does not check the other booking rules: a slot
    /// outside office hours simply fails when the booking is sent.
    /// </summary>
    public async Task<IReadOnlyList<RoomResponse>> HandleAsync(SearchAvailabilityQuery query, CancellationToken cancellationToken)
    {
        var from = query.From!.Value;
        var to = query.To!.Value;
        var capacity = Math.Max(query.Attendees ?? 1, query.MinCapacity ?? 1);

        var rooms = db.Rooms.AsNoTracking()
            .Where(room => room.IsActive && room.Site!.IsActive && room.Capacity >= capacity)
            .Where(room => !db.Bookings.Any(booking =>
                booking.RoomId == room.Id
                && booking.Status == BookingStatus.Confirmed
                && booking.BlockedFrom < to.AddMinutes(room.BufferAfterMinutes)
                && booking.BlockedUntil > from.AddMinutes(-room.BufferBeforeMinutes)))
            .Where(room => !db.RoomBlocks.Any(block => block.RoomId == room.Id && block.StartAt < to && block.EndAt > from));

        if (query.Floor is { } floor)
        {
            rooms = rooms.Where(room => room.Floor == floor);
        }

        if (query.SiteId is { } siteId)
        {
            rooms = rooms.Where(room => room.SiteId == siteId);
        }

        rooms = FeatureFilter.Apply(rooms, query.Features);

        var found = await rooms.Include(room => room.Site).OrderBy(room => room.Floor).ThenBy(room => room.Name).ToListAsync(cancellationToken);

        // A site that is closed on the day of the slot has no room to offer. The days are those of each site's own
        // time zone, so the holidays are read with one day to spare on each side and compared in memory.
        var holidays = await db.SiteHolidays.AsNoTracking()
            .Where(holiday => holiday.Date >= DateOnly.FromDateTime(from.UtcDateTime.AddDays(-1)) && holiday.Date <= DateOnly.FromDateTime(to.UtcDateTime.AddDays(1)))
            .Select(holiday => new { holiday.SiteId, holiday.Date })
            .ToListAsync(cancellationToken);

        if (holidays.Count > 0)
        {
            var closed = holidays.Select(holiday => (holiday.SiteId, holiday.Date)).ToHashSet();
            found = [.. found.Where(room =>
                !closed.Contains((room.SiteId, SiteClosures.LocalDay(room.Site!.TimeZoneId, from)))
                && !closed.Contains((room.SiteId, SiteClosures.LocalDay(room.Site!.TimeZoneId, to))))];
        }

        return found.Select(RoomResponse.From).ToList();
    }
}