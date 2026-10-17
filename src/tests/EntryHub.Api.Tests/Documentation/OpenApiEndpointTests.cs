using System.Net;
using System.Text.Json;

using EntryHub.Api.Security;
using EntryHub.Api.Tests.Support;

namespace EntryHub.Api.Tests.Documentation;

/// <summary>
/// The API contract is served only to Facilities outside Development, and the interactive page does not exist there.
/// </summary>
[Trait("Category", "Integration")]
public class OpenApiEndpointTests
{
    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/openapi/v1.yaml")]
    public async Task Returns_401_to_anonymous_callers(string path)
    {
        using var api = await TestApi.StartAsync();

        var response = await api.Anonymous().GetAsync(path, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Returns_403_to_employees()
    {
        using var api = await TestApi.StartAsync();

        var response = await api.ClientFor("ana", Roles.Employee).GetAsync("/openapi/v1.json", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Returns_the_yaml_document_to_facilities()
    {
        using var api = await TestApi.StartAsync();

        var response = await api.ClientFor("fran", Roles.Facilities).GetAsync("/openapi/v1.yaml", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("openapi:", await response.Content.ReadAsStringAsync(CancellationToken.None), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Describes_the_bearer_scheme_and_locks_only_the_protected_operations()
    {
        using var api = await TestApi.StartAsync();

        var json = await api.ClientFor("fran", Roles.Facilities).GetStringAsync("/openapi/v1.json", CancellationToken.None);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal("bearer", root.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("scheme").GetString());
        Assert.True(root.GetProperty("paths").GetProperty("/api/v1/rooms").GetProperty("post").TryGetProperty("security", out _));
        Assert.False(root.GetProperty("paths").GetProperty("/api/v1/health").GetProperty("get").TryGetProperty("security", out _));
    }

    [Fact]
    public async Task Does_not_serve_the_interactive_page_outside_development()
    {
        using var api = await TestApi.StartAsync();

        var response = await api.ClientFor("fran", Roles.Facilities).GetAsync("/scalar/v1", CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}