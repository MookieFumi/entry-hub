using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Features.V1.Bookings;
using EntryHub.Api.Features.V1.Bookings.CreateBooking;
using EntryHub.Api.Features.V1.Common;
using EntryHub.Api.Features.V1.Rooms;
using EntryHub.Api.Features.V1.Rooms.CreateRoom;
using EntryHub.Api.Security;
using EntryHub.Api.Tests.Support;

namespace EntryHub.Api.Tests.Features.V1.Bookings;

/// <summary>
/// Layer 3: the HTTP contract of the booking slices: status codes, authorization and the shape of the errors.
/// </summary>
[Trait("Category", "Integration")]
public class BookingsEndpointTests
{
    private static readonly DateTimeOffset Tuesday = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero); // 10:00 local

    private static string UniqueUser(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static async Task<RoomResponse> NewRoomAsync(TestApi api, int capacity = 8)
    {
        using var facilities = api.ClientFor(UniqueUser("facilities"), Roles.Facilities);
        var response = await facilities.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest($"Room {Guid.NewGuid():N}", 1, capacity, null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RoomResponse>())!;
    }

    private static CreateBookingRequest Request(Guid roomId, DateTimeOffset? start = null, int attendees = 4) =>
        new(roomId, "Planning", start ?? Tuesday, (start ?? Tuesday).AddHours(1), attendees);

    [Fact]
    public async Task Anonymous_and_role_less_callers_cannot_use_bookings()
    {
        using var api = await TestApi.StartAsync();
        using var anonymous = api.Anonymous();
        using var visitor = api.ClientFor("visitor");

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/bookings/mine")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await visitor.GetAsync("/api/v1/bookings/mine")).StatusCode);
    }

    [Fact]
    public async Task Post_creates_the_booking_and_it_can_be_read_back()
    {
        using var api = await TestApi.StartAsync();
        var room = await NewRoomAsync(api);
        var ana = UniqueUser("ana");
        using var client = api.ClientFor(ana, Roles.Employee);

        var created = await client.PostAsJsonAsync("/api/v1/bookings", Request(room.Id));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var booking = await created.Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options);
        Assert.Equal($"/api/v1/bookings/{booking!.Id}", created.Headers.Location?.ToString());
        Assert.Equal(ana, booking.OrganizerId);

        var fetched = await client.GetFromJsonAsync<BookingResponse>(created.Headers.Location, TestJson.Options);
        Assert.Equal(booking.Id, fetched!.Id);
    }

    [Fact]
    public async Task Post_with_a_malformed_body_returns_400_with_the_failing_fields()
    {
        using var api = await TestApi.StartAsync();
        var room = await NewRoomAsync(api);
        using var client = api.ClientFor(UniqueUser("ana"), Roles.Employee);

        var response = await client.PostAsJsonAsync(
            "/api/v1/bookings",
            new CreateBookingRequest(room.Id, "", Tuesday, Tuesday.AddHours(1), 0));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["Title", "Attendees"], await ErrorFieldsAsync(response));
    }

    [Fact]
    public async Task Post_with_an_end_before_the_start_returns_400()
    {
        using var api = await TestApi.StartAsync();
        var room = await NewRoomAsync(api);
        using var client = api.ClientFor(UniqueUser("ana"), Roles.Employee);

        var response = await client.PostAsJsonAsync(
            "/api/v1/bookings",
            new CreateBookingRequest(room.Id, "Planning", Tuesday, Tuesday.AddHours(-1), 4));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["EndAt"], await ErrorFieldsAsync(response));
    }

    private static async Task<List<string>> ErrorFieldsAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return [.. body.RootElement.GetProperty("errors").EnumerateObject().Select(property => property.Name)];
    }

    [Fact]
    public async Task Post_that_breaks_a_business_rule_returns_422_with_the_violations()
    {
        using var api = await TestApi.StartAsync();
        var room = await NewRoomAsync(api, capacity: 4);
        using var client = api.ClientFor(UniqueUser("ana"), Roles.Employee);
        var saturday = new DateTimeOffset(2026, 10, 24, 8, 0, 0, TimeSpan.Zero);

        var response = await client.PostAsJsonAsync("/api/v1/bookings", Request(room.Id, saturday, attendees: 9));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var codes = body.RootElement.GetProperty("violations").EnumerateArray()
            .Select(violation => violation.GetProperty("code").GetString())
            .ToList();
        Assert.Equal([BookingRuleCodes.OutsideOfficeHours, BookingRuleCodes.ExceedsCapacity], codes);
    }

    [Fact]
    public async Task Post_on_an_occupied_slot_returns_409()
    {
        using var api = await TestApi.StartAsync();
        var room = await NewRoomAsync(api);
        using var ana = api.ClientFor(UniqueUser("ana"), Roles.Employee);
        using var bea = api.ClientFor(UniqueUser("bea"), Roles.Employee);
        (await ana.PostAsJsonAsync("/api/v1/bookings", Request(room.Id))).EnsureSuccessStatusCode();

        var clash = await bea.PostAsJsonAsync("/api/v1/bookings", Request(room.Id, Tuesday.AddMinutes(30)));

        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);
    }

    [Fact]
    public async Task Cancel_returns_204_and_only_the_owner_or_facilities_may_do_it()
    {
        using var api = await TestApi.StartAsync();
        var room = await NewRoomAsync(api);
        using var ana = api.ClientFor(UniqueUser("ana"), Roles.Employee);
        using var bea = api.ClientFor(UniqueUser("bea"), Roles.Employee);
        var created = await ana.PostAsJsonAsync("/api/v1/bookings", Request(room.Id));
        var booking = (await created.Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options))!;

        Assert.Equal(HttpStatusCode.Forbidden, (await bea.PostAsync($"/api/v1/bookings/{booking.Id}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ana.PostAsync($"/api/v1/bookings/{booking.Id}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ana.PostAsync($"/api/v1/bookings/{Guid.CreateVersion7()}/cancel", null)).StatusCode);

        var afterwards = await ana.GetFromJsonAsync<BookingResponse>($"/api/v1/bookings/{booking.Id}", TestJson.Options);
        Assert.Equal(BookingStatus.Cancelled, afterwards!.Status);
    }

    [Fact]
    public async Task Enums_travel_as_text()
    {
        using var api = await TestApi.StartAsync();
        var room = await NewRoomAsync(api);
        using var client = api.ClientFor(UniqueUser("ana"), Roles.Employee);
        var created = await client.PostAsJsonAsync("/api/v1/bookings", Request(room.Id));

        using var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());

        Assert.Equal("Confirmed", body.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Mine_lists_only_the_bookings_of_the_caller()
    {
        using var api = await TestApi.StartAsync();
        var room = await NewRoomAsync(api);
        using var ana = api.ClientFor(UniqueUser("ana"), Roles.Employee);
        using var bea = api.ClientFor(UniqueUser("bea"), Roles.Employee);
        (await ana.PostAsJsonAsync("/api/v1/bookings", Request(room.Id))).EnsureSuccessStatusCode();
        (await bea.PostAsJsonAsync("/api/v1/bookings", Request(room.Id, Tuesday.AddHours(2)))).EnsureSuccessStatusCode();

        var mine = await ana.GetFromJsonAsync<PagedResponse<BookingResponse>>("/api/v1/bookings/mine", TestJson.Options);

        var single = Assert.Single(mine!.Items);
        Assert.Equal(Tuesday, single.StartAt);
        Assert.Equal(1, mine.TotalCount);
    }
}