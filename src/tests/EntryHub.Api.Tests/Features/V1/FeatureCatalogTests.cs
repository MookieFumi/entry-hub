using System.Net;
using System.Net.Http.Json;

using EntryHub.Api.Domain.Features;
using EntryHub.Api.Features.V1.Common;
using EntryHub.Api.Features.V1.Features;
using EntryHub.Api.Features.V1.Features.CreateFeature;
using EntryHub.Api.Features.V1.Features.UpdateFeature;
using EntryHub.Api.Features.V1.Rooms;
using EntryHub.Api.Features.V1.Rooms.CreateRoom;
using EntryHub.Api.Features.V1.Rooms.UpdateRoom;
using EntryHub.Api.Security;
using EntryHub.Api.Tests.Support;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Tests.Features.V1;

/// <summary>
/// The equipment and accessibility catalog. Every test works in a tenant of its own, so entries never leak between tests.
/// </summary>
[Trait("Category", "Integration")]
public class FeatureCatalogTests
{
    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static async Task<Guid> NewTenantWithSiteAsync()
    {
        await TestDatabase.EnsureMigratedAsync();
        var tenant = Guid.NewGuid();
        await using var db = TestDatabase.CreateContext(tenantId: tenant);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO tenants (\"Id\", \"Name\", \"CreatedAt\") VALUES ({tenant}, {Unique("Tenant")}, now())");
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO sites ("Id", "TenantId", "Name", "TimeZoneId", "Opens", "Closes", "OpenDays", "IsActive", "CreatedAt")
            VALUES ({Guid.NewGuid()}, {tenant}, 'Office', 'Europe/Madrid', '08:00', '20:00', ARRAY[1, 2, 3, 4, 5], true, now())
            """);
        return tenant;
    }

    private static async Task<FeatureResponse> NewFeatureAsync(HttpClient facilities, string name, FeatureKind kind = FeatureKind.Equipment)
    {
        var response = await facilities.PostAsJsonAsync("/api/v1/features", new CreateFeatureRequest(name, kind));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<FeatureResponse>(TestJson.Options))!;
    }

    private static async Task<RoomResponse> NewRoomAsync(HttpClient facilities, CreateRoomRequest request)
    {
        var response = await facilities.PostAsJsonAsync("/api/v1/rooms", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RoomResponse>(TestJson.Options))!;
    }

    [Fact]
    public async Task Employees_read_the_catalog_and_only_facilities_write_it()
    {
        var tenant = await NewTenantWithSiteAsync();
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientForTenant(tenant, Unique("fran"), Roles.Facilities);
        using var employee = api.ClientForTenant(tenant, Unique("ana"), Roles.Employee);
        var projector = await NewFeatureAsync(facilities, "Projector");

        var listed = await employee.GetFromJsonAsync<List<FeatureResponse>>("/api/v1/features", TestJson.Options);

        Assert.Equal(("projector", FeatureKind.Equipment), (projector.Code, projector.Kind));
        Assert.Equal([projector.Id], listed!.Select(item => item.Id));
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PostAsJsonAsync("/api/v1/features", new CreateFeatureRequest("Screen", FeatureKind.Equipment))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PatchAsJsonAsync($"/api/v1/features/{projector.Id}", new UpdateFeatureRequest("X", null, null))).StatusCode);
    }

    [Fact]
    public async Task A_name_is_unique_per_tenant_ignoring_case_but_two_tenants_can_share_it()
    {
        var first = await NewTenantWithSiteAsync();
        var second = await NewTenantWithSiteAsync();
        using var api = await TestApi.StartAsync();
        using var firstFacilities = api.ClientForTenant(first, Unique("fran"), Roles.Facilities);
        using var secondFacilities = api.ClientForTenant(second, Unique("lola"), Roles.Facilities);
        await NewFeatureAsync(firstFacilities, "Video conferencing");

        var again = await firstFacilities.PostAsJsonAsync("/api/v1/features", new CreateFeatureRequest("VIDEO CONFERENCING", FeatureKind.Equipment));
        var blank = await firstFacilities.PostAsJsonAsync("/api/v1/features", new CreateFeatureRequest("???", FeatureKind.Equipment));

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        await NewFeatureAsync(secondFacilities, "Video conferencing");
    }

    [Fact]
    public async Task An_entry_in_use_can_only_be_deactivated_and_the_room_keeps_it()
    {
        var tenant = await NewTenantWithSiteAsync();
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientForTenant(tenant, Unique("fran"), Roles.Facilities);
        using var employee = api.ClientForTenant(tenant, Unique("ana"), Roles.Employee);
        var loop = await NewFeatureAsync(facilities, "Induction loop", FeatureKind.Accessibility);
        var room = await NewRoomAsync(facilities, new CreateRoomRequest(Unique("Room"), 1, 6, null, FeatureIds: [loop.Id]));

        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await facilities.DeleteAsync($"/api/v1/features/{loop.Id}")).StatusCode);

        var retired = await facilities.PatchAsJsonAsync($"/api/v1/features/{loop.Id}", new UpdateFeatureRequest(null, null, false));
        Assert.Equal(HttpStatusCode.OK, retired.StatusCode);

        var visible = await employee.GetFromJsonAsync<List<FeatureResponse>>("/api/v1/features", TestJson.Options);
        var all = await facilities.GetFromJsonAsync<List<FeatureResponse>>("/api/v1/features?includeInactive=true", TestJson.Options);
        var stored = await facilities.GetFromJsonAsync<RoomResponse>($"/api/v1/rooms/{room.Id}", TestJson.Options);
        var another = await facilities.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(Unique("Room"), 1, 6, null, FeatureIds: [loop.Id]));

        Assert.Empty(visible!);
        Assert.Single(all!);
        Assert.Equal([loop.Id], stored!.Features.Select(item => item.Id));
        Assert.Equal(HttpStatusCode.BadRequest, another.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await facilities.PatchAsJsonAsync($"/api/v1/features/{Guid.CreateVersion7()}", new UpdateFeatureRequest("Z", null, null))).StatusCode);
    }

    [Fact]
    public async Task A_room_takes_catalog_ids_and_old_style_names_and_answers_with_both()
    {
        var tenant = await NewTenantWithSiteAsync();
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientForTenant(tenant, Unique("fran"), Roles.Facilities);
        var screen = await NewFeatureAsync(facilities, "Screen");

        var room = await NewRoomAsync(facilities, new CreateRoomRequest(Unique("Room"), 1, 6, [" Projector ", "SCREEN"], FeatureIds: [screen.Id]));
        var catalog = await facilities.GetFromJsonAsync<List<FeatureResponse>>("/api/v1/features", TestJson.Options);

        Assert.Equal(["Projector", "Screen"], room.Equipment.Order());
        Assert.Equal(2, room.Features.Count);
        Assert.Equal(["Projector", "Screen"], catalog!.Select(item => item.Name).Order());
        Assert.Contains(room.Features, item => item.Id == screen.Id);

        var replaced = await facilities.PatchAsJsonAsync($"/api/v1/rooms/{room.Id}", new UpdateRoomRequest(null, null, null, ["Whiteboard"], null));
        var kept = await facilities.PatchAsJsonAsync($"/api/v1/rooms/{room.Id}", new UpdateRoomRequest(null, 2, null, null, null));

        Assert.Equal(["Whiteboard"], (await replaced.Content.ReadFromJsonAsync<RoomResponse>(TestJson.Options))!.Equipment);
        Assert.Equal(["Whiteboard"], (await kept.Content.ReadFromJsonAsync<RoomResponse>(TestJson.Options))!.Equipment);
    }

    [Fact]
    public async Task An_id_from_another_tenant_or_an_unknown_one_is_a_400()
    {
        var mine = await NewTenantWithSiteAsync();
        var theirs = await NewTenantWithSiteAsync();
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientForTenant(mine, Unique("fran"), Roles.Facilities);
        using var foreigner = api.ClientForTenant(theirs, Unique("eve"), Roles.Facilities);
        var foreign = await NewFeatureAsync(foreigner, "Projector");
        var room = await NewRoomAsync(facilities, new CreateRoomRequest(Unique("Room"), 1, 6, null));

        var create = await facilities.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(Unique("Room"), 1, 6, null, FeatureIds: [foreign.Id]));
        var update = await facilities.PatchAsJsonAsync($"/api/v1/rooms/{room.Id}", new UpdateRoomRequest(null, null, null, null, null, FeatureIds: [Guid.CreateVersion7()]));

        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
    }

    [Fact]
    public async Task The_features_filter_needs_every_code_in_the_list_for_rooms_and_for_availability()
    {
        var tenant = await NewTenantWithSiteAsync();
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientForTenant(tenant, Unique("fran"), Roles.Facilities);
        var both = await NewRoomAsync(facilities, new CreateRoomRequest(Unique("Room"), 1, 6, ["Projector", "Whiteboard", "Screen"]));
        var oneOnly = await NewRoomAsync(facilities, new CreateRoomRequest(Unique("Room"), 1, 6, ["Projector"]));
        await NewRoomAsync(facilities, new CreateRoomRequest(Unique("Room"), 1, 6, null));
        var from = Uri.EscapeDataString(new DateTimeOffset(2035, 3, 12, 9, 0, 0, TimeSpan.Zero).ToString("O"));
        var to = Uri.EscapeDataString(new DateTimeOffset(2035, 3, 12, 10, 0, 0, TimeSpan.Zero).ToString("O"));

        var projector = await facilities.GetFromJsonAsync<PagedResponse<RoomResponse>>("/api/v1/rooms?features=projector", TestJson.Options);
        var projectorAndBoard = await facilities.GetFromJsonAsync<PagedResponse<RoomResponse>>("/api/v1/rooms?features=Projector,whiteboard", TestJson.Options);
        var free = await facilities.GetFromJsonAsync<List<RoomResponse>>($"/api/v1/rooms/availability?from={from}&to={to}&features=projector,whiteboard", TestJson.Options);
        var none = await facilities.GetFromJsonAsync<PagedResponse<RoomResponse>>("/api/v1/rooms?features=elevator", TestJson.Options);

        Assert.Equal([both.Id, oneOnly.Id], projector!.Items.Select(room => room.Id).Order());
        Assert.Equal([both.Id], projectorAndBoard!.Items.Select(room => room.Id));
        Assert.Equal([both.Id], free!.Select(room => room.Id));
        Assert.Empty(none!.Items);
    }
}