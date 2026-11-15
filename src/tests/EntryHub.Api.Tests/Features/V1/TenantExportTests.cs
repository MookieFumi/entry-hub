using System.Net;
using System.Net.Http.Json;

using EntryHub.Api.Features.V1.Audit;
using EntryHub.Api.Features.V1.Bookings.CreateBooking;
using EntryHub.Api.Features.V1.Common;
using EntryHub.Api.Features.V1.Rooms;
using EntryHub.Api.Features.V1.Rooms.CreateRoom;
using EntryHub.Api.Features.V1.Tenant.Export;
using EntryHub.Api.Security;
using EntryHub.Api.Tests.Support;

namespace EntryHub.Api.Tests.Features.V1;

/// <summary>
/// The export of everything a tenant has stored.
/// </summary>
[Trait("Category", "Integration")]
public class TenantExportTests
{
    private static readonly DateTimeOffset Tuesday = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    [Fact]
    public async Task Only_facilities_can_export()
    {
        using var api = await TestApi.StartAsync();
        using var employee = api.ClientFor(Unique("ana"), Roles.Employee);
        using var anonymous = api.Anonymous();

        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync("/api/v1/tenant/export")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/tenant/export")).StatusCode);
    }

    [Fact]
    public async Task The_export_holds_what_the_tenant_has_and_nothing_of_another_tenant()
    {
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        using var ana = api.ClientFor(Unique("ana"), Roles.Employee);
        using var otherFacilities = api.ClientForTenant(TestTenants.Other, Unique("olga"), Roles.Facilities);
        var room = (await (await facilities.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(Unique("Room"), 1, 8, ["Projector"]))).Content.ReadFromJsonAsync<RoomResponse>(TestJson.Options))!;
        var otherRoom = (await (await otherFacilities.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(Unique("Room"), 1, 8, null))).Content.ReadFromJsonAsync<RoomResponse>(TestJson.Options))!;
        var booking = await ana.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(room.Id, "Planning", Tuesday, Tuesday.AddHours(1), 4));
        booking.EnsureSuccessStatusCode();

        var export = (await facilities.GetFromJsonAsync<TenantExportResponse>("/api/v1/tenant/export", TestJson.Options))!;
        var otherExport = (await otherFacilities.GetFromJsonAsync<TenantExportResponse>("/api/v1/tenant/export", TestJson.Options))!;

        Assert.Equal(TestTenants.Default, export.TenantId);
        Assert.Contains(room.Id, export.Rooms.Select(item => item.Id));
        Assert.DoesNotContain(otherRoom.Id, export.Rooms.Select(item => item.Id));
        Assert.Contains(export.Bookings, item => item.RoomId == room.Id && item.Title == "Planning");
        Assert.NotEmpty(export.Sites);
        Assert.Contains(export.Features, item => item.Name == "Projector");
        Assert.NotEmpty(export.Audit);
        Assert.Equal(TestTenants.Other, otherExport.TenantId);
        Assert.Contains(otherRoom.Id, otherExport.Rooms.Select(item => item.Id));
        Assert.DoesNotContain(room.Id, otherExport.Rooms.Select(item => item.Id));
        Assert.DoesNotContain(otherExport.Bookings, item => item.RoomId == room.Id);
    }

    [Fact]
    public async Task Every_export_is_recorded_in_the_audit_trail()
    {
        using var api = await TestApi.StartAsync();
        var fran = Unique("fran");
        using var facilities = api.ClientFor(fran, Roles.Facilities);

        (await facilities.GetAsync("/api/v1/tenant/export")).EnsureSuccessStatusCode();

        var trail = await facilities.GetFromJsonAsync<PagedResponse<AuditResponse>>($"/api/v1/audit?entityType=Tenant&actorId={fran}", TestJson.Options);
        var entry = Assert.Single(trail!.Items);
        Assert.Equal(("Exported", TestTenants.Default), (entry.Action, entry.EntityId));
    }
}