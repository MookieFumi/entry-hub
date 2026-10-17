using EntryHub.Api.Data;
using EntryHub.Api.Features.V1.Health.GetHealth;
using EntryHub.Api.Tests.Support;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Tests.Features.V1.Health;

/// <summary>
/// Handler tests run the handler without HTTP against a real PostgreSQL.
/// </summary>
[Trait("Category", "Integration")]
public class GetHealthHandlerTests
{
    [Fact]
    public async Task Reports_healthy_when_postgres_answers()
    {
        await using var db = CreateContext(TestDatabase.ConnectionString);
        var handler = new GetHealthHandler(db);

        var response = await handler.HandleAsync(CancellationToken.None);

        Assert.Equal("Healthy", response.Status);
        Assert.Equal("Reachable", response.Database);
    }

    [Fact]
    public async Task Reports_unhealthy_when_postgres_does_not_answer()
    {
        await using var db = CreateContext(TestDatabase.Unreachable);
        var handler = new GetHealthHandler(db);

        var response = await handler.HandleAsync(CancellationToken.None);

        Assert.Equal("Unhealthy", response.Status);
        Assert.Equal("Unreachable", response.Database);
    }

    private static EntryHubDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<EntryHubDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new EntryHubDbContext(options);
    }
}