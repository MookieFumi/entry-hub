using EntryHub.Api.Data;
using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Features.V1.Common;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Features.V1.Rooms.GetRoomSchedule;

public sealed class GetRoomScheduleHandler(EntryHubDbContext db)
{
    /// <returns>The confirmed bookings of the room in the window, or <see langword="null"/> when the room does not exist.</returns>
    public async Task<IReadOnlyList<RoomScheduleEntry>?> HandleAsync(
        Caller caller,
        Guid roomId,
        GetRoomScheduleQuery query,
        CancellationToken cancellationToken)
    {
        if (!await db.Rooms.AnyAsync(room => room.Id == roomId, cancellationToken))
        {
            return null;
        }

        var from = query.From!.Value;
        var to = query.To!.Value;

        var bookings = await db.Bookings.AsNoTracking()
            .Where(booking => booking.RoomId == roomId
                && booking.Status != BookingStatus.Cancelled
                && booking.StartAt < to
                && booking.EndAt > from)
            .OrderBy(booking => booking.StartAt).ThenBy(booking => booking.Id)
            .ToListAsync(cancellationToken);

        return bookings.Select(booking =>
        {
            var mine = booking.OrganizerId == caller.Id;

            return caller.IsFacilities || mine
                ? new RoomScheduleEntry(booking.Id, booking.StartAt, booking.EndAt, mine, booking.Title, booking.OrganizerId)
                : new RoomScheduleEntry(null, booking.StartAt, booking.EndAt, false, null, null);
        }).ToList();
    }
}