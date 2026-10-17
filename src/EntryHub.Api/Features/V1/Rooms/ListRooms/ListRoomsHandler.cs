using EntryHub.Api.Data;
using EntryHub.Api.Features.V1.Common;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Features.V1.Rooms.ListRooms;

public sealed class ListRoomsHandler(EntryHubDbContext db)
{
    public async Task<PagedResponse<RoomResponse>> HandleAsync(ListRoomsQuery query, CancellationToken cancellationToken)
    {
        var paging = Paging.Normalize(query.Page, query.PageSize);

        var rooms = db.Rooms.AsNoTracking().Where(room => room.IsActive);

        if (query.Floor is { } floor)
        {
            rooms = rooms.Where(room => room.Floor == floor);
        }

        if (query.MinCapacity is { } minCapacity)
        {
            rooms = rooms.Where(room => room.Capacity >= minCapacity);
        }

        var total = await rooms.CountAsync(cancellationToken);

        var page = await rooms
            .OrderBy(room => room.Floor).ThenBy(room => room.Name)
            .Skip(paging.Skip).Take(paging.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<RoomResponse>(page.Select(RoomResponse.From).ToList(), paging.Page, paging.PageSize, total);
    }
}