using System.Net;
using System.Net.Http.Json;

using EntryHub.Api.Domain.Webhooks;
using EntryHub.Api.Features.V1.Audit;
using EntryHub.Api.Features.V1.Common;
using EntryHub.Api.Features.V1.Webhooks;
using EntryHub.Api.Security;
using EntryHub.Api.Tests.Support;

namespace EntryHub.Api.Tests.Features.V1;

/// <summary>
/// Webhook subscriptions: who manages them, what URLs are accepted and what stays private.
/// </summary>
[Trait("Category", "Integration")]
public class WebhooksTests
{
    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static CreateWebhookRequest Valid(string url = "https://hooks.example.com/entry-hub") =>
        new(url, [WebhookEvents.BookingCreated, WebhookEvents.BookingCancelled]);

    [Fact]
    public async Task Facilities_create_list_disable_and_delete_a_subscription_and_the_secret_is_shown_once()
    {
        var tenant = await TestDatabase.CreateTenantAsync(Unique("tenant"));
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientForTenant(tenant, Unique("fran"), Roles.Facilities);

        var created = await facilities.PostAsJsonAsync("/api/v1/webhooks", Valid());

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var webhook = (await created.Content.ReadFromJsonAsync<WebhookCreatedResponse>(TestJson.Options))!;
        Assert.StartsWith("whsec_", webhook.Secret);
        Assert.True(webhook.IsEnabled);
        Assert.Equal([WebhookEvents.BookingCancelled, WebhookEvents.BookingCreated], webhook.Events);

        var listing = await facilities.GetStringAsync("/api/v1/webhooks");
        Assert.Contains(webhook.Id.ToString(), listing);
        Assert.DoesNotContain(webhook.Secret, listing);
        Assert.DoesNotContain("secret", listing, StringComparison.OrdinalIgnoreCase);

        var paused = await facilities.PatchAsJsonAsync($"/api/v1/webhooks/{webhook.Id}", new UpdateWebhookRequest(false));
        Assert.False((await paused.Content.ReadFromJsonAsync<WebhookResponse>(TestJson.Options))!.IsEnabled);

        Assert.Equal(HttpStatusCode.NoContent, (await facilities.DeleteAsync($"/api/v1/webhooks/{webhook.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await facilities.DeleteAsync($"/api/v1/webhooks/{webhook.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await facilities.PatchAsJsonAsync($"/api/v1/webhooks/{webhook.Id}", new UpdateWebhookRequest(true))).StatusCode);
    }

    [Fact]
    public async Task Employees_cannot_manage_webhooks_and_anonymous_requests_are_rejected()
    {
        using var api = await TestApi.StartAsync();
        using var employee = api.ClientFor(Unique("ana"), Roles.Employee);
        using var anonymous = api.Anonymous();

        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PostAsJsonAsync("/api/v1/webhooks", Valid())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync("/api/v1/webhooks")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.DeleteAsync($"/api/v1/webhooks/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/webhooks")).StatusCode);
    }

    [Theory]
    [InlineData("http://hooks.example.com/x")]
    [InlineData("ftp://hooks.example.com/x")]
    [InlineData("/relative")]
    [InlineData("https://user:pass@hooks.example.com/x")]
    [InlineData("https://localhost/x")]
    [InlineData("https://service.localhost/x")]
    [InlineData("https://printer.local/x")]
    [InlineData("https://intranet/x")]
    [InlineData("https://127.0.0.1/x")]
    [InlineData("https://10.1.2.3/x")]
    [InlineData("https://172.16.0.1/x")]
    [InlineData("https://192.168.1.10/x")]
    [InlineData("https://169.254.169.254/latest/meta-data")]
    [InlineData("https://100.64.0.1/x")]
    [InlineData("https://0.0.0.0/x")]
    [InlineData("https://[::1]/x")]
    [InlineData("https://[fe80::1]/x")]
    [InlineData("https://[fd00::1]/x")]
    [InlineData("https://[::ffff:10.0.0.1]/x")]
    public async Task Urls_that_are_not_public_https_are_rejected(string url)
    {
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);

        var response = await facilities.PostAsJsonAsync("/api/v1/webhooks", Valid(url));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("https://hooks.example.com/x")]
    [InlineData("https://8.8.8.8/x")]
    [InlineData("https://[2606:4700:4700::1111]/x")]
    public void Public_https_urls_are_accepted(string url) => Assert.Null(WebhookUrlGuard.Validate(url));

    [Fact]
    public async Task Events_must_be_known_and_at_least_one()
    {
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);

        var unknown = await facilities.PostAsJsonAsync("/api/v1/webhooks", new CreateWebhookRequest("https://hooks.example.com/x", ["booking.exploded"]));
        var none = await facilities.PostAsJsonAsync("/api/v1/webhooks", new CreateWebhookRequest("https://hooks.example.com/x", []));

        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, none.StatusCode);
    }

    [Fact]
    public async Task A_tenant_has_a_limit_of_subscriptions()
    {
        var tenant = await TestDatabase.CreateTenantAsync(Unique("tenant"));
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientForTenant(tenant, Unique("fran"), Roles.Facilities);

        for (var index = 0; index < WebhookSubscription.MaxPerTenant; index++)
        {
            (await facilities.PostAsJsonAsync("/api/v1/webhooks", Valid($"https://hooks.example.com/{index}"))).EnsureSuccessStatusCode();
        }

        var over = await facilities.PostAsJsonAsync("/api/v1/webhooks", Valid("https://hooks.example.com/one-more"));

        Assert.Equal(HttpStatusCode.Conflict, over.StatusCode);
    }

    [Fact]
    public async Task Another_tenant_does_not_see_or_change_a_subscription()
    {
        var first = await TestDatabase.CreateTenantAsync(Unique("tenant"));
        var second = await TestDatabase.CreateTenantAsync(Unique("tenant"));
        using var api = await TestApi.StartAsync();
        using var owner = api.ClientForTenant(first, Unique("fran"), Roles.Facilities);
        using var other = api.ClientForTenant(second, Unique("fran"), Roles.Facilities);
        var webhook = (await (await owner.PostAsJsonAsync("/api/v1/webhooks", Valid())).Content.ReadFromJsonAsync<WebhookCreatedResponse>(TestJson.Options))!;

        Assert.Equal("[]", (await other.GetStringAsync("/api/v1/webhooks")).Trim());
        Assert.Equal(HttpStatusCode.NotFound, (await other.PatchAsJsonAsync($"/api/v1/webhooks/{webhook.Id}", new UpdateWebhookRequest(false))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/v1/webhooks/{webhook.Id}")).StatusCode);
        Assert.Single((await owner.GetFromJsonAsync<List<WebhookResponse>>("/api/v1/webhooks", TestJson.Options))!);
    }

    [Fact]
    public async Task Changes_are_audited_without_the_secret()
    {
        var tenant = await TestDatabase.CreateTenantAsync(Unique("tenant"));
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientForTenant(tenant, Unique("fran"), Roles.Facilities);
        var webhook = (await (await facilities.PostAsJsonAsync("/api/v1/webhooks", Valid())).Content.ReadFromJsonAsync<WebhookCreatedResponse>(TestJson.Options))!;
        (await facilities.PatchAsJsonAsync($"/api/v1/webhooks/{webhook.Id}", new UpdateWebhookRequest(false))).EnsureSuccessStatusCode();
        (await facilities.DeleteAsync($"/api/v1/webhooks/{webhook.Id}")).EnsureSuccessStatusCode();

        var audit = await facilities.GetFromJsonAsync<PagedResponse<AuditResponse>>($"/api/v1/audit?entityId={webhook.Id}", TestJson.Options);

        Assert.Equal(["Created", "Deleted", "Updated"], audit!.Items.Select(entry => entry.Action).Order(StringComparer.Ordinal));
        Assert.DoesNotContain(audit.Items, entry => entry.Summary?.Contains(webhook.Secret, StringComparison.Ordinal) == true);
    }
}