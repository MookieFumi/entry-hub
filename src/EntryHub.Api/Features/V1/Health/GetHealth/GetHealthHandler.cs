using EntryHub.Api.Data;

namespace EntryHub.Api.Features.V1.Health.GetHealth;

/// <summary>
/// Reports whether the API is running and whether PostgreSQL answers.
/// </summary>
public sealed class GetHealthHandler(EntryHubDbContext db)
{
    public async Task<GetHealthResponse> HandleAsync(CancellationToken cancellationToken)
    {
        var canConnect = await CanConnectAsync(cancellationToken);

        return canConnect
            ? new GetHealthResponse("Healthy", "Reachable")
            : new GetHealthResponse("Unhealthy", "Unreachable");
    }

    private async Task<bool> CanConnectAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await db.Database.CanConnectAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The database did not answer in time.
            return false;
        }
    }
}