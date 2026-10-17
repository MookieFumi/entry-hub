using System.Net;
using System.Net.Http.Json;

using EntryHub.Api.Features.V1.Health.GetHealth;
using EntryHub.Api.Tests.Support;

namespace EntryHub.Api.Tests.Features.V1.Health;

/// <summary>
/// Endpoint tests cover routing, the <c>/api/v1</c> prefix and the HTTP status codes only.
/// </summary>
[Trait("Category", "Integration")]
public class GetHealthEndpointTests
{
    [Fact]
    public async Task Returns_200_when_postgres_answers()
    {
        await using var factory = new ApiFactory(TestDatabase.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/health", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GetHealthResponse>(CancellationToken.None);
        Assert.Equal("Healthy", body?.Status);
    }

    [Fact]
    public async Task Returns_503_when_postgres_does_not_answer()
    {
        await using var factory = new ApiFactory(TestDatabase.Unreachable);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/health", CancellationToken.None);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}