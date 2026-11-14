using System.Net;
using System.Net.Http.Json;

using EntryHub.Api.Features.V1.Common;
using EntryHub.Api.Features.V1.Rooms;
using EntryHub.Api.Features.V1.Rooms.CreateRoom;
using EntryHub.Api.Security;
using EntryHub.Api.Tests.Support;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Tests.Tenancy;

/// <summary>
/// One customer must never see, change or collide with the data of another. These tests call the real API with
/// tokens of two tenants and look at what each one gets back.
/// </summary>
[Trait("Category", "Integration")]
public class TenantIsolationTests
{
    private static string UniqueName() => $"Room {Guid.NewGuid():N}";

    [Fact]
    public async Task A_room_is_invisible_to_another_tenant()
    {
        using var api = await TestApi.StartAsync();
        using var owner = api.ClientForTenant(TestTenants.Default, "fran", Roles.Facilities);
        using var stranger = api.ClientForTenant(TestTenants.Other, "eva", Roles.Facilities);

        var created = await owner.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(UniqueName(), 77, 6, null));
        var room = await created.Content.ReadFromJsonAsync<RoomResponse>(TestJson.Options);
        Assert.NotNull(room);

        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/v1/rooms/{room.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/v1/rooms/{room.Id}")).StatusCode);

        var theirList = await stranger.GetFromJsonAsync<PagedResponse<RoomResponse>>("/api/v1/rooms?floor=77", TestJson.Options);
        Assert.NotNull(theirList);
        Assert.DoesNotContain(theirList.Items, item => item.Id == room.Id);
    }

    [Fact]
    public async Task Two_tenants_can_use_the_same_room_name()
    {
        using var api = await TestApi.StartAsync();
        using var first = api.ClientForTenant(TestTenants.Default, "fran", Roles.Facilities);
        using var second = api.ClientForTenant(TestTenants.Other, "eva", Roles.Facilities);
        var name = UniqueName();

        Assert.Equal(HttpStatusCode.Created, (await first.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(name, 1, 4, null))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await second.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(name, 1, 4, null))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await second.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(name, 1, 4, null))).StatusCode);
    }

    [Fact]
    public async Task A_booking_cannot_be_made_in_a_room_of_another_tenant()
    {
        using var api = await TestApi.StartAsync();
        using var owner = api.ClientForTenant(TestTenants.Default, "fran", Roles.Facilities);
        using var stranger = api.ClientForTenant(TestTenants.Other, "eva", Roles.Employee);

        var created = await owner.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(UniqueName(), 78, 6, null));
        var room = await created.Content.ReadFromJsonAsync<RoomResponse>(TestJson.Options);
        Assert.NotNull(room);

        var start = TestClock.Default.GetUtcNow().AddDays(1);
        var response = await stranger.PostAsJsonAsync(
            "/api/v1/bookings",
            new { roomId = room.Id, title = "Not mine", startAt = start, endAt = start.AddHours(1), attendees = 2 });

        // The room does not exist for that tenant, so the rule "room unavailable" answers, never a booking.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task A_token_without_a_tenant_gets_403_everywhere()
    {
        using var api = await TestApi.StartAsync();
        using var client = api.ClientWithToken(TestTokens.Sign(TestTokens.Describe("fran", [Roles.Facilities], null)));

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/rooms")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/bookings/mine")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(UniqueName(), 1, 4, null))).StatusCode);
    }

    [Fact]
    public async Task A_token_with_a_malformed_tenant_gets_403()
    {
        using var api = await TestApi.StartAsync();
        var descriptor = TestTokens.Describe("fran", [Roles.Facilities], null);
        descriptor.Subject.AddClaim(new System.Security.Claims.Claim("tenant_id", "not-a-guid"));
        using var client = api.ClientWithToken(TestTokens.Sign(descriptor));

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/rooms")).StatusCode);
    }

    [Fact]
    public async Task A_context_without_a_tenant_sees_nothing_and_cannot_write()
    {
        await TestDatabase.EnsureMigratedAsync();
        await using var tenantless = new EntryHub.Api.Data.EntryHubDbContext(
            new DbContextOptionsBuilder<EntryHub.Api.Data.EntryHubDbContext>().UseNpgsql(TestDatabase.ConnectionString).Options);

        Assert.Empty(await tenantless.Rooms.ToListAsync());

        tenantless.Rooms.Add(new EntryHub.Api.Domain.Rooms.Room { Id = Guid.CreateVersion7(), Name = UniqueName(), Capacity = 1 });
        await Assert.ThrowsAsync<InvalidOperationException>(() => tenantless.SaveChangesAsync());
    }

    [Fact]
    public async Task Rows_are_stamped_with_the_tenant_of_the_request()
    {
        await TestDatabase.EnsureMigratedAsync();
        await using var db = TestDatabase.CreateContext(tenantId: TestTenants.Other);
        var room = new EntryHub.Api.Domain.Rooms.Room { Id = Guid.CreateVersion7(), Name = UniqueName(), Capacity = 3, CreatedAt = DateTimeOffset.UtcNow };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        Assert.Equal(TestTenants.Other, room.TenantId);

        await using var otherTenant = TestDatabase.CreateContext(tenantId: TestTenants.Default);
        Assert.False(await otherTenant.Rooms.AnyAsync(r => r.Id == room.Id));
    }
}