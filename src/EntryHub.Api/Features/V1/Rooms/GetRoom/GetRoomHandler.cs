using EntryHub.Api.Data;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Features.V1.Rooms.GetRoom;

public sealed class GetRoomHandler(EntryHubDbContext db)
{
    public async Task<RoomResponse?> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var room = await db.Rooms.AsNoTracking().SingleOrDefaultAsync(room => room.Id == id, cancellationToken);

        return room is null ? null : RoomResponse.From(room);
    }
}