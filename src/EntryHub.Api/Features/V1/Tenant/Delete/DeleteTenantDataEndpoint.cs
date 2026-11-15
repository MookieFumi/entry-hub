using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace EntryHub.Api.Features.V1.Tenant.Delete;

public static class DeleteTenantDataEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapDelete("/data", HandleAsync).WithName("DeleteTenantData")
            .WithSummary("Permanently deletes everything the tenant has stored: bookings, rooms, sites, holidays, catalog, blocks, rules and the audit trail. It all happens in one transaction. The tenant itself and one audit entry about the deletion stay. Facilities only; the body must confirm with the name of the tenant. Export the data first.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static async Task<Results<Ok<DeleteTenantDataResponse>, ProblemHttpResult, NotFound>> HandleAsync(
        [FromBody] DeleteTenantDataRequest request,
        DeleteTenantDataHandler handler,
        CancellationToken cancellationToken)
    {
        var (outcome, result) = await handler.HandleAsync(request, cancellationToken);

        return outcome switch
        {
            DeleteTenantDataOutcome.Deleted => TypedResults.Ok(result!),
            DeleteTenantDataOutcome.NotConfirmed => TypedResults.Problem(
                title: "Deletion not confirmed",
                detail: "Send { \"confirmation\": \"DELETE <name of the tenant>\" } to confirm that all the data must be deleted.",
                statusCode: StatusCodes.Status400BadRequest),
            _ => TypedResults.NotFound(),
        };
    }
}