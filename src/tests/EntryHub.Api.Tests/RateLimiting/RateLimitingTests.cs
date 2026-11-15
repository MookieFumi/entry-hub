using System.Net;
using System.Net.Http.Json;

using EntryHub.Api.Security;
using EntryHub.Api.Tests.Support;

using Microsoft.AspNetCore.Mvc;

namespace EntryHub.Api.Tests.RateLimiting;

/// <summary>
/// One tenant cannot use up the capacity of the others, and the platform probes are never limited.
/// </summary>
[Trait("Category", "Integration")]
public class RateLimitingTests
{
    private const string FrontendOrigin = "https://entryhub-web.pages.dev";

    private static Dictionary<string, string?> Settings(int permits, int anonymous = 1000)
    {
        var settings = new Dictionary<string, string?>(TestTokens.Configuration)
        {
            ["RateLimiting:PermitLimit"] = permits.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["RateLimiting:AnonymousPermitLimit"] = anonymous.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["RateLimiting:WindowSeconds"] = "60",
            ["Cors:AllowedOrigins:0"] = FrontendOrigin,
        };
        return settings;
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    [Fact]
    public async Task A_tenant_that_goes_over_its_limit_gets_429_and_another_tenant_does_not()
    {
        using var api = await TestApi.StartAsync(Settings(permits: 3));
        var busyTenant = Guid.NewGuid();
        var quietTenant = Guid.NewGuid();
        using var busy = api.ClientForTenant(busyTenant, Unique("ana"), Roles.Employee);
        using var otherUserOfBusy = api.ClientForTenant(busyTenant, Unique("luis"), Roles.Employee);
        using var quiet = api.ClientForTenant(quietTenant, Unique("eva"), Roles.Employee);

        var statuses = new List<HttpStatusCode>();
        for (var request = 0; request < 3; request++)
        {
            statuses.Add((await busy.GetAsync("/api/v1/rules")).StatusCode);
        }

        // The limit belongs to the tenant, so a second user of the same tenant is counted with the first.
        var overLimit = await otherUserOfBusy.GetAsync("/api/v1/rules");
        var untouched = await quiet.GetAsync("/api/v1/rules");

        Assert.All(statuses, status => Assert.NotEqual(HttpStatusCode.TooManyRequests, status));
        Assert.Equal(HttpStatusCode.TooManyRequests, overLimit.StatusCode);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, untouched.StatusCode);
    }

    [Fact]
    public async Task The_429_is_a_problem_with_retry_after_and_the_cors_headers_so_the_browser_can_read_it()
    {
        using var api = await TestApi.StartAsync(Settings(permits: 1));
        using var client = api.ClientForTenant(Guid.NewGuid(), Unique("ana"), Roles.Employee);
        client.DefaultRequestHeaders.Add("Origin", FrontendOrigin);
        await client.GetAsync("/api/v1/rules");

        var response = await client.GetAsync("/api/v1/rules");

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(429, problem!.Status);
        Assert.True(response.Headers.RetryAfter?.Delta?.TotalSeconds is > 0 and <= 60);
        Assert.Equal(FrontendOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task Anonymous_callers_are_limited_by_their_own_smaller_limit()
    {
        using var api = await TestApi.StartAsync(Settings(permits: 1000, anonymous: 2));
        using var anonymous = api.Anonymous();

        var first = await anonymous.GetAsync("/api/v1/me");
        var second = await anonymous.GetAsync("/api/v1/me");
        var third = await anonymous.GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
    }

    [Fact]
    public async Task The_platform_probes_are_never_limited()
    {
        using var api = await TestApi.StartAsync(Settings(permits: 1, anonymous: 1));
        using var client = api.Anonymous();

        for (var request = 0; request < 5; request++)
        {
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await client.GetAsync("/alive")).StatusCode);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await client.GetAsync("/health")).StatusCode);
        }
    }

    [Fact]
    public async Task The_limiter_can_be_turned_off()
    {
        var settings = Settings(permits: 1);
        settings["RateLimiting:Enabled"] = "false";
        using var api = await TestApi.StartAsync(settings);
        using var client = api.ClientForTenant(Guid.NewGuid(), Unique("ana"), Roles.Employee);

        for (var request = 0; request < 5; request++)
        {
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await client.GetAsync("/api/v1/rules")).StatusCode);
        }
    }
}