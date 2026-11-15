using EntryHub.Api.Data;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Features.V1.Rooms.Blocks;

public sealed class DeleteBlockHandler(EntryHubDbContext db)
{
    /// <returns><see langword="false"/> when the block does not exist in that room.</returns>
    public async Task<bool> HandleAsync(Guid roomId, Guid blockId, CancellationToken cancellationToken)
    {
        var block = await db.RoomBlocks.SingleOrDefaultAsync(block => block.Id == blockId && block.RoomId == roomId, cancellationToken);

        if (block is null)
        {
            return false;
        }

        db.RoomBlocks.Remove(block);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}