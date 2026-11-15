using EntryHub.Api.Data;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Features.V1.Rooms.Blocks;

public sealed class ListBlocksHandler(EntryHubDbContext db)
{
    /// <returns><see langword="null"/> when the room does not exist.</returns>
    public async Task<IReadOnlyList<RoomBlockResponse>?> HandleAsync(Guid roomId, ListBlocksQuery query, CancellationToken cancellationToken)
    {
        if (!await db.Rooms.AnyAsync(room => room.Id == roomId, cancellationToken))
        {
            return null;
        }

        var blocks = db.RoomBlocks.AsNoTracking().Where(block => block.RoomId == roomId);

        if (query.From is { } from)
        {
            blocks = blocks.Where(block => block.EndAt > from);
        }

        if (query.To is { } to)
        {
            blocks = blocks.Where(block => block.StartAt < to);
        }

        return await blocks.OrderBy(block => block.StartAt).ThenBy(block => block.Id)
            .Select(block => RoomBlockResponse.From(block)).ToListAsync(cancellationToken);
    }
}