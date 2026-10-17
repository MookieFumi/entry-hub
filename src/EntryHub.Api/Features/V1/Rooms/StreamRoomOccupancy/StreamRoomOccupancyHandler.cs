using System.Runtime.CompilerServices;

using EntryHub.Api.Data;
using EntryHub.Api.Domain.Rooms;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EntryHub.Api.Features.V1.Rooms.StreamRoomOccupancy;

public sealed class StreamRoomOccupancyHandler(
    EntryHubDbContext db,
    TimeProvider clock,
    IOptions<StreamRoomOccupancyOptions> options)
{
    /// <returns>A stream of snapshots, or <see langword="null"/> when the room does not exist.</returns>
    public async Task<IAsyncEnumerable<RoomOccupancy>?> HandleAsync(Guid roomId, CancellationToken cancellationToken)
    {
        if (!await db.Rooms.AnyAsync(room => room.Id == roomId, cancellationToken))
        {
            return null;
        }

        return StreamAsync(roomId, cancellationToken);
    }

    private async IAsyncEnumerable<RoomOccupancy> StreamAsync(
        Guid roomId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(options.Value.Interval, clock);

        do
        {
            // Bookings arrive in a later change; until then a room is never busy.
            yield return OccupancyCalculator.Calculate(roomId, [], clock.GetUtcNow());
        }
        while (await timer.WaitForNextTickAsync(cancellationToken));
    }
}