using System.Net;
using System.Net.Http.Headers;

using EntryHub.Api.Tests.Support;

namespace EntryHub.Api.Tests.Cors;

/// <summary>
/// The browser front end lives on another origin, so the API must answer its preflight and mark its responses.
/// </summary>
[Trait("Category", "Integration")]
public class CorsTests
{
    private const string Frontend = "https://entryhub-web.pages.dev";
    private const string Preview = "https://4f2c9d1.entryhub-web.pages.dev";
    private const string Stranger = "https://evil.example.com";

    private static Dictionary<string, string?> Settings(params string[] origins)
    {
        var settings = new Dictionary<string, string?>(TestTokens.Configuration);
        for (var index = 0; index < origins.Length; index++)
        {
            settings[$"Cors:AllowedOrigins:{index}"] = origins[index];
        }

        return settings;
    }

    private static HttpRequestMessage Preflight(string origin, string path = "/api/v1/bookings")
    {
        var request = new HttpRequestMessage(HttpMethod.Options, path);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
        return request;
    }

    private static string? AllowOrigin(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values) ? values.Single() : null;

    [Fact]
    public async Task The_preflight_of_an_allowed_origin_is_answered_without_a_token()
    {
        using var api = await TestApi.StartAsync(Settings(Frontend));
        using var client = api.Anonymous();

        var response = await client.SendAsync(Preflight(Frontend));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(Frontend, AllowOrigin(response));
        Assert.Contains("POST", response.Headers.GetValues("Access-Control-Allow-Methods").Single());
        var allowedHeaders = response.Headers.GetValues("Access-Control-Allow-Headers").Single();
        Assert.Contains("authorization", allowedHeaders, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("content-type", allowedHeaders, StringComparison.OrdinalIgnoreCase);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task An_origin_that_is_not_listed_gets_no_cors_headers()
    {
        using var api = await TestApi.StartAsync(Settings(Frontend));
        using var client = api.Anonymous();

        var response = await client.SendAsync(Preflight(Stranger));

        Assert.Null(AllowOrigin(response));
    }

    [Fact]
    public async Task With_no_origins_configured_nobody_is_allowed()
    {
        using var api = await TestApi.StartAsync(Settings());
        using var client = api.Anonymous();

        Assert.Null(AllowOrigin(await client.SendAsync(Preflight(Frontend))));
    }

    [Fact]
    public async Task A_wildcard_subdomain_allows_the_preview_deployments_and_nothing_else()
    {
        using var api = await TestApi.StartAsync(Settings("https://*.entryhub-web.pages.dev"));
        using var client = api.Anonymous();

        Assert.Equal(Preview, AllowOrigin(await client.SendAsync(Preflight(Preview))));
        Assert.Null(AllowOrigin(await client.SendAsync(Preflight("https://entryhub-web.pages.dev.evil.example.com"))));
        Assert.Null(AllowOrigin(await client.SendAsync(Preflight(Stranger))));
    }

    [Fact]
    public async Task A_401_still_carries_the_cors_header_so_the_browser_can_read_it()
    {
        using var api = await TestApi.StartAsync(Settings(Frontend));
        using var client = api.Anonymous();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/rooms");
        request.Headers.Add("Origin", Frontend);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(Frontend, AllowOrigin(response));
    }

    [Fact]
    public async Task An_authenticated_call_from_the_front_end_carries_the_cors_header()
    {
        using var api = await TestApi.StartAsync(Settings(Frontend));
        using var client = api.ClientFor("ana", EntryHub.Api.Security.Roles.Employee);
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/rooms");
        request.Headers.Add("Origin", Frontend);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Frontend, AllowOrigin(response));
    }

    [Fact]
    public async Task A_503_keeps_the_cors_header()
    {
        await using var factory = new ApiFactory(TestDatabase.Unreachable, Settings(Frontend));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Create("ana", EntryHub.Api.Security.Roles.Employee));
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/rooms");
        request.Headers.Add("Origin", Frontend);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(Frontend, AllowOrigin(response));
    }

    [Fact]
    public async Task A_lone_star_stops_the_api_from_starting_instead_of_opening_it_to_every_page()
    {
        using var api = await TestApi.StartAsync(Settings("*"));

        var exception = Assert.Throws<InvalidOperationException>(() => api.Anonymous());

        Assert.Contains("Cors:AllowedOrigins", exception.Message, StringComparison.Ordinal);
    }
}