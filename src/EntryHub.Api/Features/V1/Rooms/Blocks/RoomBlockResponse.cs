using EntryHub.Api.Domain.Rooms;

namespace EntryHub.Api.Features.V1.Rooms.Blocks;

/// <summary>
/// A time range in which a room cannot be booked.
/// </summary>
/// <param name="Id">Identifier of the block.</param>
/// <param name="RoomId">The blocked room.</param>
/// <param name="StartAt">First moment of the block.</param>
/// <param name="EndAt">The block ends here; this moment is free again.</param>
/// <param name="Reason">Why the room is blocked, such as "Cleaning".</param>
/// <param name="CreatedAt">When the block was created.</param>
public sealed record RoomBlockResponse(Guid Id, Guid RoomId, DateTimeOffset StartAt, DateTimeOffset EndAt, string? Reason, DateTimeOffset CreatedAt)
{
    public static RoomBlockResponse From(RoomBlock block) => new(block.Id, block.RoomId, block.StartAt, block.EndAt, block.Reason, block.CreatedAt);
}