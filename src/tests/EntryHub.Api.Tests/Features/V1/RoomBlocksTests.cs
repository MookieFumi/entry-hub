using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using EntryHub.Api.Features.V1.Bookings;
using EntryHub.Api.Features.V1.Bookings.CreateBooking;
using EntryHub.Api.Features.V1.Bookings.RescheduleBooking;
using EntryHub.Api.Features.V1.Rooms;
using EntryHub.Api.Features.V1.Rooms.Blocks;
using EntryHub.Api.Features.V1.Rooms.CreateRoom;
using EntryHub.Api.Security;
using EntryHub.Api.Tests.Support;

namespace EntryHub.Api.Tests.Features.V1;

/// <summary>
/// Maintenance blocks of a room: who manages them and how they keep the room from being booked.
/// </summary>
[Trait("Category", "Integration")]
public class RoomBlocksTests
{
    private static readonly DateTimeOffset Tuesday = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static async Task<RoomResponse> NewRoomAsync(HttpClient facilities)
    {
        var response = await facilities.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(Unique("Room"), 1, 8, null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RoomResponse>(TestJson.Options))!;
    }

    private static CreateBookingRequest Booking(Guid roomId, DateTimeOffset start) => new(roomId, "Planning", start, start.AddHours(1), 4);

    [Fact]
    public async Task Only_facilities_manage_blocks_and_everyone_can_list_them()
    {
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        using var employee = api.ClientFor(Unique("ana"), Roles.Employee);
        var room = await NewRoomAsync(facilities);
        var request = new CreateBlockRequest(Tuesday, Tuesday.AddHours(2), " Cleaning ");

        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PostAsJsonAsync($"/api/v1/rooms/{room.Id}/blocks", request)).StatusCode);
        var created = await facilities.PostAsJsonAsync($"/api/v1/rooms/{room.Id}/blocks", request);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var block = (await created.Content.ReadFromJsonAsync<RoomBlockResponse>(TestJson.Options))!;
        Assert.Equal("Cleaning", block.Reason);
        var listed = await employee.GetFromJsonAsync<List<RoomBlockResponse>>($"/api/v1/rooms/{room.Id}/blocks", TestJson.Options);
        Assert.Equal([block.Id], listed!.Select(item => item.Id));
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.DeleteAsync($"/api/v1/rooms/{room.Id}/blocks/{block.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await facilities.DeleteAsync($"/api/v1/rooms/{room.Id}/blocks/{block.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await facilities.DeleteAsync($"/api/v1/rooms/{room.Id}/blocks/{block.Id}")).StatusCode);
    }

    [Fact]
    public async Task A_block_needs_an_existing_room_and_a_valid_range()
    {
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        var room = await NewRoomAsync(facilities);

        var unknown = await facilities.PostAsJsonAsync($"/api/v1/rooms/{Guid.NewGuid()}/blocks", new CreateBlockRequest(Tuesday, Tuesday.AddHours(1), null));
        var backwards = await facilities.PostAsJsonAsync($"/api/v1/rooms/{room.Id}/blocks", new CreateBlockRequest(Tuesday, Tuesday.AddHours(-1), null));

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, backwards.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await facilities.GetAsync($"/api/v1/rooms/{Guid.NewGuid()}/blocks")).StatusCode);
    }

    [Fact]
    public async Task The_list_can_be_limited_to_a_window()
    {
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        var room = await NewRoomAsync(facilities);

        foreach (var day in new[] { 0, 7, 14 })
        {
            (await facilities.PostAsJsonAsync($"/api/v1/rooms/{room.Id}/blocks", new CreateBlockRequest(Tuesday.AddDays(day), Tuesday.AddDays(day).AddHours(1), null))).EnsureSuccessStatusCode();
        }

        var from = Uri.EscapeDataString(Tuesday.AddDays(3).ToString("O"));
        var to = Uri.EscapeDataString(Tuesday.AddDays(10).ToString("O"));
        var middle = await facilities.GetFromJsonAsync<List<RoomBlockResponse>>($"/api/v1/rooms/{room.Id}/blocks?from={from}&to={to}", TestJson.Options);

        Assert.Equal([Tuesday.AddDays(7)], middle!.Select(item => item.StartAt));
    }

    [Fact]
    public async Task A_block_over_confirmed_bookings_is_refused_with_their_ids_and_ignores_cancelled_ones()
    {
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        using var ana = api.ClientFor(Unique("ana"), Roles.Employee);
        var room = await NewRoomAsync(facilities);
        var booking = (await (await ana.PostAsJsonAsync("/api/v1/bookings", Booking(room.Id, Tuesday))).Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options))!;
        var request = new CreateBlockRequest(Tuesday.AddMinutes(30), Tuesday.AddHours(3), "Repairs");

        var refused = await facilities.PostAsJsonAsync($"/api/v1/rooms/{room.Id}/blocks", request);

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        using var body = JsonDocument.Parse(await refused.Content.ReadAsStringAsync());
        Assert.Equal(booking.Id, body.RootElement.GetProperty("bookingIds")[0].GetGuid());

        (await facilities.PostAsync($"/api/v1/bookings/{booking.Id}/cancel", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Created, (await facilities.PostAsJsonAsync($"/api/v1/rooms/{room.Id}/blocks", request)).StatusCode);
    }

    [Fact]
    public async Task A_blocked_room_cannot_be_booked_or_rescheduled_into_and_is_free_again_after_the_block_is_lifted()
    {
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        using var ana = api.ClientFor(Unique("ana"), Roles.Employee);
        var room = await NewRoomAsync(facilities);
        var other = await NewRoomAsync(facilities);
        var blockResponse = await facilities.PostAsJsonAsync($"/api/v1/rooms/{room.Id}/blocks", new CreateBlockRequest(Tuesday, Tuesday.AddHours(2), "Cleaning"));
        var block = (await blockResponse.Content.ReadFromJsonAsync<RoomBlockResponse>(TestJson.Options))!;
        var elsewhere = (await (await ana.PostAsJsonAsync("/api/v1/bookings", Booking(other.Id, Tuesday))).Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options))!;

        var refused = await ana.PostAsJsonAsync("/api/v1/bookings", Booking(room.Id, Tuesday.AddHours(1)));
        var moved = await ana.PostAsJsonAsync($"/api/v1/bookings/{elsewhere.Id}/reschedule", new RescheduleBookingRequest(Tuesday.AddHours(1), Tuesday.AddHours(2), room.Id));
        var from = Uri.EscapeDataString(Tuesday.ToString("O"));
        var to = Uri.EscapeDataString(Tuesday.AddHours(1).ToString("O"));
        var found = await ana.GetFromJsonAsync<List<RoomResponse>>($"/api/v1/rooms/availability?from={from}&to={to}", TestJson.Options);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Contains("RoomBlocked", await refused.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, moved.StatusCode);
        Assert.Contains("RoomBlocked", await moved.Content.ReadAsStringAsync());
        Assert.DoesNotContain(found!, item => item.Id == room.Id);

        (await facilities.DeleteAsync($"/api/v1/rooms/{room.Id}/blocks/{block.Id}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Created, (await ana.PostAsJsonAsync("/api/v1/bookings", Booking(room.Id, Tuesday.AddHours(1)))).StatusCode);
    }

    [Fact]
    public async Task A_booking_right_after_a_block_is_allowed()
    {
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        using var ana = api.ClientFor(Unique("ana"), Roles.Employee);
        var room = await NewRoomAsync(facilities);
        (await facilities.PostAsJsonAsync($"/api/v1/rooms/{room.Id}/blocks", new CreateBlockRequest(Tuesday, Tuesday.AddHours(1), null))).EnsureSuccessStatusCode();

        var response = await ana.PostAsJsonAsync("/api/v1/bookings", Booking(room.Id, Tuesday.AddHours(1)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}