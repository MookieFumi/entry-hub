using EntryHub.Api.Data;
using EntryHub.Api.Domain.Bookings;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Features.V1.Rooms.Blocks;

/// <summary>
/// Asks whether a room is blocked while a booking would take place. The booking policy has no database, so the
/// handlers that book or reschedule call this next to it.
/// </summary>
internal static class RoomBlocks
{
    /// <returns>The violation when a block overlaps the range, or <see langword="null"/> when the room is free of blocks.</returns>
    public static async Task<RuleViolation?> CheckAsync(
        EntryHubDbContext db,
        Guid roomId,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken)
    {
        var from = start.ToUniversalTime();
        var to = end.ToUniversalTime();

        var block = await db.RoomBlocks.AsNoTracking()
            .Where(block => block.RoomId == roomId && block.StartAt < to && block.EndAt > from)
            .OrderBy(block => block.StartAt)
            .Select(block => new { block.Reason })
            .FirstOrDefaultAsync(cancellationToken);

        return block is null
            ? null
            : new(BookingRuleCodes.RoomBlocked, $"The room is blocked at that time{(block.Reason is null ? "" : $" ({block.Reason})")}.");
    }
}