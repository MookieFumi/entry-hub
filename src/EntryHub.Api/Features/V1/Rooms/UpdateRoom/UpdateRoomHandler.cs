using EntryHub.Api.Data;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace EntryHub.Api.Features.V1.Rooms.UpdateRoom;

public enum UpdateRoomOutcome
{
    Updated,
    NotFound,
    NameInUse,
}

public sealed class UpdateRoomHandler(EntryHubDbContext db)
{
    public async Task<(UpdateRoomOutcome Outcome, RoomResponse? Room)> HandleAsync(
        Guid id,
        UpdateRoomRequest request,
        CancellationToken cancellationToken)
    {
        var room = await db.Rooms.SingleOrDefaultAsync(room => room.Id == id, cancellationToken);

        if (room is null)
        {
            return (UpdateRoomOutcome.NotFound, null);
        }

        if (request.Name?.Trim() is { Length: > 0 } name && name != room.Name)
        {
            if (await db.Rooms.AnyAsync(other => other.Name == name && other.Id != id, cancellationToken))
            {
                return (UpdateRoomOutcome.NameInUse, null);
            }

            room.Name = name;
        }

        room.Floor = request.Floor ?? room.Floor;
        room.Capacity = request.Capacity ?? room.Capacity;
        room.IsActive = request.IsActive ?? room.IsActive;

        if (request.Equipment is not null)
        {
            room.Equipment = request.Equipment.Select(item => item.Trim()).Where(item => item.Length > 0).Distinct().ToList();
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return (UpdateRoomOutcome.NameInUse, null);
        }

        return (UpdateRoomOutcome.Updated, RoomResponse.From(room));
    }
}