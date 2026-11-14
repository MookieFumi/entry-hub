using System.Net;
using System.Net.Http.Json;

using EntryHub.Api.Features.V1.Bookings;
using EntryHub.Api.Features.V1.Bookings.CreateBooking;
using EntryHub.Api.Features.V1.Common;
using EntryHub.Api.Features.V1.CurrentUser.GetMe;
using EntryHub.Api.Features.V1.Rooms;
using EntryHub.Api.Features.V1.Rooms.CreateRoom;
using EntryHub.Api.Features.V1.Rooms.GetRoomSchedule;
using EntryHub.Api.Features.V1.Rooms.UpdateRoom;
using EntryHub.Api.Features.V1.Rules.GetRules;
using EntryHub.Api.Security;
using EntryHub.Api.Tests.Support;

namespace EntryHub.Api.Tests.Features.V1;

/// <summary>
/// The endpoints a front end needs besides creating and listing: who am I, the rules, editing rooms, who is free
/// and what a room's day looks like.
/// </summary>
[Trait("Category", "Integration")]
public class FrontEndEndpointsTests
{
    private static readonly DateTimeOffset Tuesday = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static async Task<RoomResponse> NewRoomAsync(TestApi api, int capacity = 8, int floor = 1)
    {
        using var facilities = api.ClientFor(Unique("facilities"), Roles.Facilities);
        var response = await facilities.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest($"Room {Guid.NewGuid():N}", floor, capacity, null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RoomResponse>(TestJson.Options))!;
    }

    private static async Task<BookingResponse> BookAsync(HttpClient client, Guid roomId, DateTimeOffset start, string title = "Planning")
    {
        var response = await client.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(roomId, title, start, start.AddHours(1), 4));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options))!;
    }

    [Fact]
    public async Task Me_says_who_is_calling_and_which_tenant_they_belong_to()
    {
        using var api = await TestApi.StartAsync();
        var user = Unique("fran");
        using var client = api.ClientFor(user, Roles.Facilities);

        var me = await client.GetFromJsonAsync<MeResponse>("/api/v1/me", TestJson.Options);

        Assert.Equal(user, me!.UserId);
        Assert.Equal(Roles.Facilities, me.Role);
        Assert.Equal(TestTenants.Default, me.TenantId);
        Assert.False(string.IsNullOrWhiteSpace(me.TenantName));
    }

    [Fact]
    public async Task Me_needs_a_token_and_a_role()
    {
        using var api = await TestApi.StartAsync();
        using var anonymous = api.Anonymous();
        using var visitor = api.ClientFor("visitor");

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await visitor.GetAsync("/api/v1/me")).StatusCode);
    }

    [Fact]
    public async Task Rules_are_the_ones_the_server_enforces()
    {
        using var api = await TestApi.StartAsync();
        using var employee = api.ClientFor(Unique("ana"), Roles.Employee);
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);

        var rules = await employee.GetFromJsonAsync<RulesResponse>("/api/v1/rules", TestJson.Options);
        var forFacilities = await facilities.GetFromJsonAsync<RulesResponse>("/api/v1/rules", TestJson.Options);

        Assert.Equal(15, rules!.MinDurationMinutes);
        Assert.Equal(240, rules.MaxDurationMinutes);
        Assert.Equal("08:00", rules.OfficeOpens);
        Assert.Equal("20:00", rules.OfficeCloses);
        Assert.Equal("Europe/Madrid", rules.TimeZone);
        Assert.Equal(5, rules.MaxActiveBookings);
        Assert.Null(forFacilities!.MaxActiveBookings);
    }

    [Fact]
    public async Task Facilities_list_every_bookings_and_employees_cannot()
    {
        using var api = await TestApi.StartAsync();
        var room = await NewRoomAsync(api);
        using var ana = api.ClientFor(Unique("ana"), Roles.Employee);
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        var booking = await BookAsync(ana, room.Id, Tuesday);

        var page = await facilities.GetFromJsonAsync<PagedResponse<BookingResponse>>($"/api/v1/bookings?roomId={room.Id}", TestJson.Options);
        var other = await facilities.GetFromJsonAsync<PagedResponse<BookingResponse>>($"/api/v1/bookings?roomId={room.Id}&status=Cancelled", TestJson.Options);

        Assert.Equal(booking.Id, Assert.Single(page!.Items).Id);
        Assert.Empty(other!.Items);
        Assert.Equal(HttpStatusCode.Forbidden, (await ana.GetAsync("/api/v1/bookings")).StatusCode);
    }

    [Fact]
    public async Task Facilities_edit_and_deactivate_a_room_and_only_they_can_see_it_afterwards()
    {
        using var api = await TestApi.StartAsync();
        var room = await NewRoomAsync(api);
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        using var ana = api.ClientFor(Unique("ana"), Roles.Employee);

        var edited = await facilities.PatchAsJsonAsync($"/api/v1/rooms/{room.Id}", new UpdateRoomRequest(null, 3, 12, ["Projector"], false));

        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var body = await edited.Content.ReadFromJsonAsync<RoomResponse>(TestJson.Options);
        Assert.Equal((3, 12, false), (body!.Floor, body.Capacity, body.IsActive));
        Assert.Equal(["Projector"], body.Equipment);
        Assert.Equal(room.Name, body.Name);

        var employeeList = await ana.GetFromJsonAsync<PagedResponse<RoomResponse>>("/api/v1/rooms?floor=3&pageSize=100", TestJson.Options);
        var facilitiesList = await facilities.GetFromJsonAsync<PagedResponse<RoomResponse>>("/api/v1/rooms?floor=3&includeInactive=true&pageSize=100", TestJson.Options);
        var employeeAsksForInactive = await ana.GetFromJsonAsync<PagedResponse<RoomResponse>>("/api/v1/rooms?floor=3&includeInactive=true&pageSize=100", TestJson.Options);

        Assert.DoesNotContain(employeeList!.Items, item => item.Id == room.Id);
        Assert.Contains(facilitiesList!.Items, item => item.Id == room.Id);
        Assert.DoesNotContain(employeeAsksForInactive!.Items, item => item.Id == room.Id);
    }

    [Fact]
    public async Task Renaming_a_room_to_a_name_in_use_gives_409_and_an_unknown_room_404_and_employees_403()
    {
        using var api = await TestApi.StartAsync();
        var first = await NewRoomAsync(api);
        var second = await NewRoomAsync(api);
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        using var ana = api.ClientFor(Unique("ana"), Roles.Employee);

        var taken = await facilities.PatchAsJsonAsync($"/api/v1/rooms/{second.Id}", new UpdateRoomRequest(first.Name, null, null, null, null));
        var missing = await facilities.PatchAsJsonAsync($"/api/v1/rooms/{Guid.NewGuid()}", new UpdateRoomRequest("Anything", null, null, null, null));
        var forbidden = await ana.PatchAsJsonAsync($"/api/v1/rooms/{first.Id}", new UpdateRoomRequest("Nope", null, null, null, null));
        var invalid = await facilities.PatchAsJsonAsync($"/api/v1/rooms/{first.Id}", new UpdateRoomRequest(null, null, 0, null, null));

        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Availability_leaves_out_booked_small_and_inactive_rooms()
    {
        using var api = await TestApi.StartAsync();
        var floor = Random.Shared.Next(40, 90);
        var free = await NewRoomAsync(api, capacity: 10, floor: floor);
        var booked = await NewRoomAsync(api, capacity: 10, floor: floor);
        var small = await NewRoomAsync(api, capacity: 2, floor: floor);
        var inactive = await NewRoomAsync(api, capacity: 10, floor: floor);
        using var ana = api.ClientFor(Unique("ana"), Roles.Employee);
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        await BookAsync(ana, booked.Id, Tuesday);
        (await facilities.PatchAsJsonAsync($"/api/v1/rooms/{inactive.Id}", new UpdateRoomRequest(null, null, null, null, false))).EnsureSuccessStatusCode();

        var from = Uri.EscapeDataString(Tuesday.AddMinutes(30).ToString("O"));
        var to = Uri.EscapeDataString(Tuesday.AddMinutes(90).ToString("O"));
        var found = await ana.GetFromJsonAsync<List<RoomResponse>>($"/api/v1/rooms/availability?from={from}&to={to}&attendees=6&floor={floor}", TestJson.Options);

        Assert.Equal([free.Id], found!.Select(room => room.Id));
        _ = small;
    }

    [Fact]
    public async Task Availability_and_schedule_reject_a_missing_or_backwards_window()
    {
        using var api = await TestApi.StartAsync();
        var room = await NewRoomAsync(api);
        using var ana = api.ClientFor(Unique("ana"), Roles.Employee);
        var from = Uri.EscapeDataString(Tuesday.ToString("O"));
        var before = Uri.EscapeDataString(Tuesday.AddHours(-1).ToString("O"));
        var tooWide = Uri.EscapeDataString(Tuesday.AddDays(40).ToString("O"));

        Assert.Equal(HttpStatusCode.BadRequest, (await ana.GetAsync("/api/v1/rooms/availability")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ana.GetAsync($"/api/v1/rooms/availability?from={from}&to={before}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ana.GetAsync($"/api/v1/rooms/{room.Id}/schedule?from={from}&to={tooWide}")).StatusCode);
    }

    [Fact]
    public async Task Schedule_shows_everyone_when_a_room_is_busy_but_only_the_organizer_and_facilities_see_the_details()
    {
        using var api = await TestApi.StartAsync();
        var room = await NewRoomAsync(api);
        var ana = Unique("ana");
        using var anaClient = api.ClientFor(ana, Roles.Employee);
        using var luis = api.ClientFor(Unique("luis"), Roles.Employee);
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        var booking = await BookAsync(anaClient, room.Id, Tuesday, "Secret merger");
        var url = $"/api/v1/rooms/{room.Id}/schedule?from={Uri.EscapeDataString(Tuesday.AddHours(-1).ToString("O"))}&to={Uri.EscapeDataString(Tuesday.AddHours(5).ToString("O"))}";

        var seenByLuis = Assert.Single((await luis.GetFromJsonAsync<List<RoomScheduleEntry>>(url, TestJson.Options))!);
        var seenByAna = Assert.Single((await anaClient.GetFromJsonAsync<List<RoomScheduleEntry>>(url, TestJson.Options))!);
        var seenByFacilities = Assert.Single((await facilities.GetFromJsonAsync<List<RoomScheduleEntry>>(url, TestJson.Options))!);

        Assert.Equal((Tuesday, Tuesday.AddHours(1)), (seenByLuis.StartAt, seenByLuis.EndAt));
        Assert.Null(seenByLuis.Title);
        Assert.Null(seenByLuis.OrganizerId);
        Assert.Null(seenByLuis.BookingId);
        Assert.False(seenByLuis.IsMine);
        Assert.Equal(("Secret merger", booking.Id, true), (seenByAna.Title, seenByAna.BookingId, seenByAna.IsMine));
        Assert.Equal(("Secret merger", ana), (seenByFacilities.Title, seenByFacilities.OrganizerId));
    }

    [Fact]
    public async Task Schedule_of_an_unknown_room_is_404()
    {
        using var api = await TestApi.StartAsync();
        using var ana = api.ClientFor(Unique("ana"), Roles.Employee);
        var from = Uri.EscapeDataString(Tuesday.ToString("O"));
        var to = Uri.EscapeDataString(Tuesday.AddHours(8).ToString("O"));

        Assert.Equal(HttpStatusCode.NotFound, (await ana.GetAsync($"/api/v1/rooms/{Guid.NewGuid()}/schedule?from={from}&to={to}")).StatusCode);
    }
}