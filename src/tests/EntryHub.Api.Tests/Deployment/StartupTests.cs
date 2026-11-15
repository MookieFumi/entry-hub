using System.Net;

using EntryHub.Api.Tests.Support;

namespace EntryHub.Api.Tests.Deployment;

/// <summary>
/// Outside Development the API must not change the schema when it starts: the deployment does it with an owner role,
/// and the API runs with an ordinary role that Row-Level Security applies to.
/// </summary>
[Trait("Category", "Integration")]
public class StartupTests
{
    [Fact]
    public async Task The_api_starts_and_serves_its_probes_without_touching_the_database()
    {
        // The database does not exist: if the API tried to migrate at startup, this would throw instead of answering.
        using var factory = new ApiFactory(TestDatabase.Unreachable);
        using var client = factory.CreateClient();

        var alive = await client.GetAsync("/alive");

        Assert.Equal(HttpStatusCode.OK, alive.StatusCode);
    }
}