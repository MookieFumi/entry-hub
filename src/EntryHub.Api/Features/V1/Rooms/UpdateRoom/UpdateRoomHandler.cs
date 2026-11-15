using EntryHub.Api.Data;
using EntryHub.Api.Features.V1.Features;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace EntryHub.Api.Features.V1.Rooms.UpdateRoom;

public enum UpdateRoomOutcome
{
    Updated,
    NotFound,
    NameInUse,
    SiteNotFound,
    FeatureNotFound,
}

public sealed class UpdateRoomHandler(EntryHubDbContext db, FeatureResolver features)
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

        var siteChanged = request.SiteId is not null && request.SiteId != room.SiteId;

        if (siteChanged && !await db.Sites.AnyAsync(site => site.Id == request.SiteId && site.IsActive, cancellationToken))
        {
            return (UpdateRoomOutcome.SiteNotFound, null);
        }

        var newSiteId = siteChanged ? request.SiteId!.Value : room.SiteId;
        var newName = request.Name?.Trim() is { Length: > 0 } trimmed ? trimmed : room.Name;

        if ((siteChanged || newName != room.Name)
            && await db.Rooms.AnyAsync(other => other.SiteId == newSiteId && other.Name == newName && other.Id != id, cancellationToken))
        {
            return (UpdateRoomOutcome.NameInUse, null);
        }

        room.SiteId = newSiteId;
        room.Name = newName;

        room.Floor = request.Floor ?? room.Floor;
        room.Capacity = request.Capacity ?? room.Capacity;
        room.BufferBeforeMinutes = request.BufferBeforeMinutes ?? room.BufferBeforeMinutes;
        room.BufferAfterMinutes = request.BufferAfterMinutes ?? room.BufferAfterMinutes;
        room.IsActive = request.IsActive ?? room.IsActive;

        var (resolved, unknownFeature) = await features.ResolveAsync(request.FeatureIds, request.Equipment, cancellationToken);

        if (unknownFeature is not null)
        {
            return (UpdateRoomOutcome.FeatureNotFound, null);
        }

        if (resolved is not null)
        {
            room.Features = resolved;
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