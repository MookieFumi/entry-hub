using System.Net;
using System.Net.Http.Json;

using EntryHub.Api.Features.V1.Audit;
using EntryHub.Api.Features.V1.Bookings.CreateBooking;
using EntryHub.Api.Features.V1.Common;
using EntryHub.Api.Features.V1.Rooms;
using EntryHub.Api.Features.V1.Rooms.CreateRoom;
using EntryHub.Api.Features.V1.Tenant.Delete;
using EntryHub.Api.Features.V1.Tenant.Export;
using EntryHub.Api.Security;
using EntryHub.Api.Tests.Support;

namespace EntryHub.Api.Tests.Features.V1;

/// <summary>
/// The deletion of everything a tenant has stored. Each test makes its own tenant, because the deletion cannot share one.
/// </summary>
[Trait("Category", "Integration")]
public class TenantDeletionTests
{
    private static readonly DateTimeOffset Tuesday = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static Task<HttpResponseMessage> DeleteAsync(HttpClient client, string? confirmation)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/tenant/data") { Content = JsonContent.Create(new DeleteTenantDataRequest(confirmation)) };
        return client.SendAsync(request);
    }

    [Fact]
    public async Task Only_facilities_can_delete_and_the_name_of_the_tenant_must_be_confirmed()
    {
        using var api = await TestApi.StartAsync();
        var name = Unique("Acme");
        var tenant = await TestDatabase.CreateTenantAsync(name);
        using var facilities = api.ClientForTenant(tenant, Unique("fran"), Roles.Facilities);
        using var employee = api.ClientForTenant(tenant, Unique("ana"), Roles.Employee);

        Assert.Equal(HttpStatusCode.Forbidden, (await DeleteAsync(employee, $"DELETE {name}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await DeleteAsync(facilities, null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await DeleteAsync(facilities, "DELETE")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await DeleteAsync(facilities, $"delete {name}")).StatusCode);
        var export = await facilities.GetFromJsonAsync<TenantExportResponse>("/api/v1/tenant/export", TestJson.Options);
        Assert.NotEmpty(export!.Sites);
    }

    [Fact]
    public async Task Deleting_removes_everything_of_the_tenant_and_nothing_of_another_one()
    {
        using var api = await TestApi.StartAsync();
        var name = Unique("Acme");
        var tenant = await TestDatabase.CreateTenantAsync(name);
        var fran = Unique("fran");
        using var facilities = api.ClientForTenant(tenant, fran, Roles.Facilities);
        using var ana = api.ClientForTenant(tenant, Unique("ana"), Roles.Employee);
        using var otherFacilities = api.ClientForTenant(TestTenants.Other, Unique("olga"), Roles.Facilities);
        var room = (await (await facilities.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(Unique("Room"), 1, 8, ["Projector"]))).Content.ReadFromJsonAsync<RoomResponse>(TestJson.Options))!;
        (await ana.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(room.Id, "Planning", Tuesday, Tuesday.AddHours(1), 4))).EnsureSuccessStatusCode();
        var otherRoom = (await (await otherFacilities.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(Unique("Room"), 1, 8, null))).Content.ReadFromJsonAsync<RoomResponse>(TestJson.Options))!;

        var deleted = await DeleteAsync(facilities, $"DELETE {name}");

        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        var counts = (await deleted.Content.ReadFromJsonAsync<DeleteTenantDataResponse>(TestJson.Options))!;
        Assert.Equal((1, 1, 1), (counts.Bookings, counts.Rooms, counts.Sites));
        var export = (await facilities.GetFromJsonAsync<TenantExportResponse>("/api/v1/tenant/export", TestJson.Options))!;
        Assert.Empty(export.Bookings);
        Assert.Empty(export.Rooms);
        Assert.Empty(export.Sites);
        Assert.Empty(export.Features);
        var otherExport = (await otherFacilities.GetFromJsonAsync<TenantExportResponse>("/api/v1/tenant/export", TestJson.Options))!;
        Assert.Contains(otherExport.Rooms, item => item.Id == otherRoom.Id);
    }

    [Fact]
    public async Task The_deletion_leaves_one_audit_entry_that_says_who_did_it()
    {
        using var api = await TestApi.StartAsync();
        var name = Unique("Acme");
        var tenant = await TestDatabase.CreateTenantAsync(name);
        var fran = Unique("fran");
        using var facilities = api.ClientForTenant(tenant, fran, Roles.Facilities);
        (await facilities.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(Unique("Room"), 1, 8, null))).EnsureSuccessStatusCode();

        (await DeleteAsync(facilities, $"DELETE {name}")).EnsureSuccessStatusCode();

        var trail = await facilities.GetFromJsonAsync<PagedResponse<AuditResponse>>("/api/v1/audit", TestJson.Options);
        var entry = Assert.Single(trail!.Items);
        Assert.Equal(("Deleted", "Tenant", fran, tenant), (entry.Action, entry.EntityType, entry.ActorId, entry.EntityId));
    }
}