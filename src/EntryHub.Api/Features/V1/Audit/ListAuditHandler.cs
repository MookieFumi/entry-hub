using EntryHub.Api.Data;
using EntryHub.Api.Features.V1.Common;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Features.V1.Audit;

public sealed class ListAuditHandler(EntryHubDbContext db)
{
    private static readonly string[] KnownTypes = ["Booking", "Room", "Site", "Rules", "Feature", "SiteHoliday", "RoomBlock"];

    public async Task<PagedResponse<AuditResponse>> HandleAsync(ListAuditQuery query, CancellationToken cancellationToken)
    {
        var paging = Paging.Normalize(query.Page, query.PageSize);
        var entries = db.AuditEntries.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            // The names are a closed set, so the match is done here and the query compares exact values.
            var type = KnownTypes.FirstOrDefault(known => string.Equals(known, query.EntityType.Trim(), StringComparison.OrdinalIgnoreCase));
            entries = entries.Where(entry => entry.EntityType == type);
        }

        if (query.EntityId is { } entityId)
        {
            entries = entries.Where(entry => entry.EntityId == entityId);
        }

        if (!string.IsNullOrWhiteSpace(query.ActorId))
        {
            var actor = query.ActorId.Trim();
            entries = entries.Where(entry => entry.ActorId == actor);
        }

        if (query.From is { } from)
        {
            entries = entries.Where(entry => entry.At >= from);
        }

        if (query.To is { } to)
        {
            entries = entries.Where(entry => entry.At < to);
        }

        var total = await entries.CountAsync(cancellationToken);

        var page = await entries
            .OrderByDescending(entry => entry.At).ThenByDescending(entry => entry.Id)
            .Skip(paging.Skip).Take(paging.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<AuditResponse>(page.Select(AuditResponse.From).ToList(), paging.Page, paging.PageSize, total);
    }
}