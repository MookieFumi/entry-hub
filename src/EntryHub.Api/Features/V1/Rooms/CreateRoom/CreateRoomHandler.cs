using EntryHub.Api.Data;
using EntryHub.Api.Domain.Rooms;
using EntryHub.Api.Features.V1.Features;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace EntryHub.Api.Features.V1.Rooms.CreateRoom;

public enum CreateRoomOutcome
{
    Created,
    NameInUse,
    SiteNotFound,
    FeatureNotFound,
}

public sealed class CreateRoomHandler(EntryHubDbContext db, FeatureResolver features, TimeProvider clock)
{
    public async Task<(CreateRoomOutcome Outcome, RoomResponse? Room)> HandleAsync(CreateRoomRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();

        var sites = db.Sites.Where(site => site.IsActive);
        var siteId = request.SiteId is { } requested
            ? await sites.Where(site => site.Id == requested).Select(site => (Guid?)site.Id).SingleOrDefaultAsync(cancellationToken)
            : await sites.OrderBy(site => site.CreatedAt).ThenBy(site => site.Id).Select(site => (Guid?)site.Id).FirstOrDefaultAsync(cancellationToken);

        if (siteId is null)
        {
            return (CreateRoomOutcome.SiteNotFound, null);
        }

        if (await db.Rooms.AnyAsync(room => room.SiteId == siteId && room.Name == name, cancellationToken))
        {
            return (CreateRoomOutcome.NameInUse, null);
        }

        var (resolved, unknownFeature) = await features.ResolveAsync(request.FeatureIds, request.Equipment ?? [], cancellationToken);

        if (unknownFeature is not null)
        {
            return (CreateRoomOutcome.FeatureNotFound, null);
        }

        var room = new Room
        {
            Id = Guid.CreateVersion7(),
            SiteId = siteId.Value,
            Name = name,
            Floor = request.Floor,
            Capacity = request.Capacity,
            BufferBeforeMinutes = request.BufferBeforeMinutes,
            BufferAfterMinutes = request.BufferAfterMinutes,
            RequiresApproval = request.RequiresApproval,
            Features = resolved ?? [],
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
            return (CreateRoomOutcome.NameInUse, null);
        }

        return (CreateRoomOutcome.Created, RoomResponse.From(room));
    }
}