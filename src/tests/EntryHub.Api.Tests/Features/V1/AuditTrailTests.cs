using System.Net;
using System.Net.Http.Json;

using EntryHub.Api.Features.V1.Audit;
using EntryHub.Api.Features.V1.Bookings;
using EntryHub.Api.Features.V1.Bookings.CreateBooking;
using EntryHub.Api.Features.V1.Bookings.RescheduleBooking;
using EntryHub.Api.Features.V1.Common;
using EntryHub.Api.Features.V1.Rooms;
using EntryHub.Api.Features.V1.Rooms.CreateRoom;
using EntryHub.Api.Features.V1.Rooms.UpdateRoom;
using EntryHub.Api.Security;
using EntryHub.Api.Tests.Support;

namespace EntryHub.Api.Tests.Features.V1;

/// <summary>
/// The audit trail: what is written, who can read it and that tenants cannot see each other's.
/// </summary>
[Trait("Category", "Integration")]
public class AuditTrailTests
{
    private static readonly DateTimeOffset Tuesday = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static async Task<List<AuditResponse>> TrailOfAsync(HttpClient client, Guid entityId)
    {
        var page = await client.GetFromJsonAsync<PagedResponse<AuditResponse>>($"/api/v1/audit?entityId={entityId}", TestJson.Options);
        return [.. page!.Items.OrderBy(entry => entry.At).ThenBy(entry => entry.Id)];
    }

    [Fact]
    public async Task Only_facilities_can_read_the_trail()
    {
        using var api = await TestApi.StartAsync();
        using var employee = api.ClientFor(Unique("ana"), Roles.Employee);
        using var anonymous = api.Anonymous();

        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync("/api/v1/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/audit")).StatusCode);
    }

    [Fact]
    public async Task Room_changes_are_recorded_with_the_actor_and_the_changed_fields()
    {
        using var api = await TestApi.StartAsync();
        var fran = Unique("fran");
        using var facilities = api.ClientFor(fran, Roles.Facilities);
        var name = Unique("Room");
        var created = await facilities.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(name, 1, 8, null));
        var room = (await created.Content.ReadFromJsonAsync<RoomResponse>(TestJson.Options))!;

        (await facilities.PatchAsJsonAsync($"/api/v1/rooms/{room.Id}", new UpdateRoomRequest(null, null, 12, null, null, null))).EnsureSuccessStatusCode();

        var trail = await TrailOfAsync(facilities, room.Id);
        Assert.Equal(["Created", "Updated"], trail.Select(entry => entry.Action));
        Assert.All(trail, entry =>
        {
            Assert.Equal(fran, entry.ActorId);
            Assert.Equal("Room", entry.EntityType);
        });
        Assert.Equal(name, trail[0].Summary);
        Assert.Contains("Capacity", trail[1].Summary);
        Assert.DoesNotContain("12", trail[1].Summary);
    }

    [Fact]
    public async Task Booking_create_reschedule_and_cancel_are_recorded()
    {
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        var ana = Unique("ana");
        using var employee = api.ClientFor(ana, Roles.Employee);
        var room = (await (await facilities.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(Unique("Room"), 1, 8, null))).Content.ReadFromJsonAsync<RoomResponse>(TestJson.Options))!;
        var booking = (await (await employee.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(room.Id, "Planning", Tuesday, Tuesday.AddHours(1), 4))).Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options))!;

        (await employee.PostAsJsonAsync($"/api/v1/bookings/{booking.Id}/reschedule", new RescheduleBookingRequest(Tuesday.AddHours(2), Tuesday.AddHours(3)))).EnsureSuccessStatusCode();
        (await employee.PostAsync($"/api/v1/bookings/{booking.Id}/cancel", null)).EnsureSuccessStatusCode();

        var trail = await TrailOfAsync(facilities, booking.Id);
        Assert.Equal(["Created", "Rescheduled", "Cancelled"], trail.Select(entry => entry.Action));
        Assert.All(trail, entry => Assert.Equal(ana, entry.ActorId));
    }

    [Fact]
    public async Task The_trail_can_be_filtered_and_pages()
    {
        using var api = await TestApi.StartAsync();
        var fran = Unique("fran");
        using var facilities = api.ClientFor(fran, Roles.Facilities);
        var room = (await (await facilities.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(Unique("Room"), 1, 8, null))).Content.ReadFromJsonAsync<RoomResponse>(TestJson.Options))!;
        (await facilities.PatchAsJsonAsync($"/api/v1/rooms/{room.Id}", new UpdateRoomRequest(null, 2, null, null, null, null))).EnsureSuccessStatusCode();

        var byActor = await facilities.GetFromJsonAsync<PagedResponse<AuditResponse>>($"/api/v1/audit?actorId={fran}&entityType=room", TestJson.Options);
        var firstPage = await facilities.GetFromJsonAsync<PagedResponse<AuditResponse>>($"/api/v1/audit?actorId={fran}&pageSize=1", TestJson.Options);

        Assert.Equal(2, byActor!.TotalCount);
        Assert.Single(firstPage!.Items);
        Assert.Equal(2, firstPage.TotalCount);
        Assert.Equal("Updated", firstPage.Items[0].Action);
    }

    [Fact]
    public async Task Tenants_do_not_see_each_others_trail()
    {
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        using var otherFacilities = api.ClientForTenant(TestTenants.Other, Unique("olga"), Roles.Facilities);
        var room = (await (await facilities.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(Unique("Room"), 1, 8, null))).Content.ReadFromJsonAsync<RoomResponse>(TestJson.Options))!;

        Assert.Single(await TrailOfAsync(facilities, room.Id));
        Assert.Empty(await TrailOfAsync(otherFacilities, room.Id));
    }
}