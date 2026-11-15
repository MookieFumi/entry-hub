using EntryHub.Api.Demo;
using EntryHub.Api.Tests.Support;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Tests.Demo;

/// <summary>
/// The seeder only runs when <c>Demo:Seed</c> says so: a database that nobody asked to fill stays as it is.
/// </summary>
[Trait("Category", "Integration")]
[Collection("demo data")]
public class DemoSeedingServiceTests
{
    private static async Task<int> RoomsOfAsync(Guid tenantId)
    {
        await using var db = TestDatabase.CreateContext(tenantId: tenantId);
        return await db.Rooms.CountAsync();
    }

    private static async Task RemoveDemoDataAsync()
    {
        await using var db = TestDatabase.CreateContext();
        await db.Database.ExecuteSqlRawAsync("""
            DELETE FROM bookings WHERE "TenantId" IN ('de000000-0000-0000-0000-0000000000a1', 'de000000-0000-0000-0000-0000000000a2');
            DELETE FROM rooms WHERE "TenantId" IN ('de000000-0000-0000-0000-0000000000a1', 'de000000-0000-0000-0000-0000000000a2');
            DELETE FROM sites WHERE "TenantId" IN ('de000000-0000-0000-0000-0000000000a1', 'de000000-0000-0000-0000-0000000000a2');
            DELETE FROM tenant_rules WHERE "TenantId" IN ('de000000-0000-0000-0000-0000000000a1', 'de000000-0000-0000-0000-0000000000a2');
            """);
    }

    [Fact]
    public async Task Without_the_setting_the_api_starts_and_seeds_nothing()
    {
        await TestDatabase.EnsureMigratedAsync();
        await RemoveDemoDataAsync();

        using var api = await TestApi.StartAsync();
        using var client = api.Anonymous();
        await client.GetAsync("/alive");
        await Task.Delay(500);

        Assert.Equal(0, await RoomsOfAsync(DemoSeeder.AcmeId));
        Assert.Equal(0, await RoomsOfAsync(DemoSeeder.GlobexId));
    }

    [Fact]
    public async Task With_the_setting_the_api_seeds_both_tenants_when_it_starts()
    {
        await TestDatabase.EnsureMigratedAsync();
        await RemoveDemoDataAsync();
        var settings = new Dictionary<string, string?>(TestTokens.Configuration) { ["Demo:Seed"] = "true" };

        using var api = await TestApi.StartAsync(settings);
        using var client = api.Anonymous();
        await client.GetAsync("/alive");

        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (await RoomsOfAsync(DemoSeeder.GlobexId) < 5 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
        }

        Assert.Equal(8, await RoomsOfAsync(DemoSeeder.AcmeId));
        Assert.Equal(5, await RoomsOfAsync(DemoSeeder.GlobexId));
    }
}