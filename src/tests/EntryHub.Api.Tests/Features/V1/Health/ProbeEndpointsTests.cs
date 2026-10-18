using System.Net;

using EntryHub.Api.Tests.Support;

namespace EntryHub.Api.Tests.Features.V1.Health;

/// <summary>
/// The Kubernetes probes call <c>/health</c> and <c>/alive</c> outside the versioned API, and the factory does not
/// run in the Development environment, so a pass here means they are mapped in production too.
/// </summary>
[Trait("Category", "Integration")]
public class ProbeEndpointsTests
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/alive")]
    public async Task Probes_answer_without_a_token_outside_development(string path)
    {
        await using var factory = new ApiFactory(TestDatabase.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}