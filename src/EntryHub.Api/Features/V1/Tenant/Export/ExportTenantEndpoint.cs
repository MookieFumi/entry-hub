using Microsoft.AspNetCore.Http.HttpResults;

namespace EntryHub.Api.Features.V1.Tenant.Export;

public static class ExportTenantEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/export", HandleAsync).WithName("ExportTenantData")
            .WithSummary("Downloads everything the tenant has stored as one JSON document: sites, holidays, catalog, rooms, blocks, rules, bookings and the audit trail. The export is itself recorded in the audit trail. Facilities only.")
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static async Task<Results<Ok<TenantExportResponse>, NotFound>> HandleAsync(ExportTenantHandler handler, CancellationToken cancellationToken) =>
        await handler.HandleAsync(cancellationToken) is { } export ? TypedResults.Ok(export) : TypedResults.NotFound();
}