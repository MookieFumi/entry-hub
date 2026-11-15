using EntryHub.Api.Data;
using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Domain.Rooms;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Features.V1.Rooms.Blocks;

public abstract record CreateBlockResult
{
    private CreateBlockResult()
    {
    }

    public sealed record Created(RoomBlockResponse Block) : CreateBlockResult;

    public sealed record RoomNotFound : CreateBlockResult;

    /// <summary>
    /// Confirmed bookings sit in the range: they have to be moved or cancelled first.
    /// </summary>
    public sealed record BookingsInTheWay(IReadOnlyList<Guid> BookingIds) : CreateBlockResult;
}

public sealed class CreateBlockHandler(EntryHubDbContext db, TimeProvider clock)
{
    public async Task<CreateBlockResult> HandleAsync(Guid roomId, CreateBlockRequest request, CancellationToken cancellationToken)
    {
        if (!await db.Rooms.AnyAsync(room => room.Id == roomId, cancellationToken))
        {
            return new CreateBlockResult.RoomNotFound();
        }

        var start = request.StartAt.ToUniversalTime();
        var end = request.EndAt.ToUniversalTime();

        var inTheWay = await db.Bookings.AsNoTracking()
            .Where(booking => booking.RoomId == roomId && booking.Status == BookingStatus.Confirmed && booking.StartAt < end && booking.EndAt > start)
            .OrderBy(booking => booking.StartAt).Select(booking => booking.Id)
            .ToListAsync(cancellationToken);

        if (inTheWay.Count > 0)
        {
            return new CreateBlockResult.BookingsInTheWay(inTheWay);
        }

        var block = new RoomBlock
        {
            Id = Guid.CreateVersion7(),
            RoomId = roomId,
            StartAt = start,
            EndAt = end,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
            CreatedAt = clock.GetUtcNow(),
        };

        db.RoomBlocks.Add(block);
        await db.SaveChangesAsync(cancellationToken);

        return new CreateBlockResult.Created(RoomBlockResponse.From(block));
    }
}