using EntryHub.Api.Data;
using EntryHub.Api.Domain.Bookings;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Features.V1.Rooms.SearchAvailability;

public sealed class SearchAvailabilityHandler(EntryHubDbContext db)
{
    /// <summary>
    /// Active rooms with no confirmed booking that overlaps the slot. It does not check the booking rules: a slot
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
                && booking.StartAt < to
                && booking.EndAt > from));

        if (query.Floor is { } floor)
        {
            rooms = rooms.Where(room => room.Floor == floor);
        }

        if (query.SiteId is { } siteId)
        {
            rooms = rooms.Where(room => room.SiteId == siteId);
        }

        var found = await rooms.OrderBy(room => room.Floor).ThenBy(room => room.Name).ToListAsync(cancellationToken);

        return found.Select(RoomResponse.From).ToList();
    }
}