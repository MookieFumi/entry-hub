using EntryHub.Api.Features.V1.Common;
using EntryHub.Api.Security;

using Microsoft.AspNetCore.Http.HttpResults;

namespace EntryHub.Api.Features.V1.Audit;

public static class AuditEndpoints
{
    public static void MapAuditEndpoints(this RouteGroupBuilder v1) =>
        v1.MapGet("/audit", HandleAsync).WithTags("Audit").WithName("ListAudit")
            .WithSummary("Lists the changes made to bookings, rooms, sites, rules, the catalog, holidays and blocks, newest first, with paging. Facilities only.")
            .RequireAuthorization(Policies.Facilities);

    private static async Task<Ok<PagedResponse<AuditResponse>>> HandleAsync(
        [AsParameters] ListAuditQuery query,
        ListAuditHandler handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(query, cancellationToken));
}