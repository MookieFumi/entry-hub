using EntryHub.Api.Data;
using EntryHub.Api.Domain.Rooms;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace EntryHub.Api.Features.V1.Rooms.CreateRoom;

public sealed class CreateRoomHandler(EntryHubDbContext db, TimeProvider clock)
{
    /// <returns>The new room, or <see langword="null"/> when another room already has that name.</returns>
    public async Task<RoomResponse?> HandleAsync(CreateRoomRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();

        if (await db.Rooms.AnyAsync(room => room.Name == name, cancellationToken))
        {
            return null;
        }

        var room = new Room
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            Floor = request.Floor,
            Capacity = request.Capacity,
            Equipment = request.Equipment?.Select(item => item.Trim()).Where(item => item.Length > 0).Distinct().ToList() ?? [],
            CreatedAt = clock.GetUtcNow(),
        };

        db.Rooms.Add(room);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two requests with the same name raced past the check above; the unique index decided.
            return null;
        }

        return RoomResponse.From(room);
    }
}