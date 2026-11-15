using System.Net;
using System.Net.Http.Json;

using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Features.V1.Bookings;
using EntryHub.Api.Features.V1.Bookings.CreateBooking;
using EntryHub.Api.Features.V1.Rooms;
using EntryHub.Api.Features.V1.Rooms.CreateRoom;
using EntryHub.Api.Security;
using EntryHub.Api.Tests.Support;

namespace EntryHub.Api.Tests.Features.V1;

/// <summary>
/// Rooms that need approval: who gets a pending booking and that a pending booking holds the room.
/// </summary>
[Trait("Category", "Integration")]
public class RoomApprovalTests
{
    private static readonly DateTimeOffset Tuesday = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static async Task<RoomResponse> NewRoomAsync(HttpClient facilities, bool requiresApproval)
    {
        var response = await facilities.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(Unique("Room"), 1, 8, null, RequiresApproval: requiresApproval));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RoomResponse>(TestJson.Options))!;
    }

    private static Task<HttpResponseMessage> BookAsync(HttpClient client, Guid roomId, DateTimeOffset start) =>
        client.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(roomId, "Planning", start, start.AddHours(1), 4));

    [Fact]
    public async Task A_room_does_not_need_approval_unless_it_is_set_and_it_can_be_changed()
    {
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        var plain = await NewRoomAsync(facilities, false);

        var updated = await facilities.PatchAsJsonAsync($"/api/v1/rooms/{plain.Id}", new { requiresApproval = true });

        Assert.False(plain.RequiresApproval);
        Assert.True((await updated.Content.ReadFromJsonAsync<RoomResponse>(TestJson.Options))!.RequiresApproval);
    }

    [Fact]
    public async Task An_employee_booking_waits_for_approval_and_a_facilities_booking_does_not()
    {
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        using var ana = api.ClientFor(Unique("ana"), Roles.Employee);
        var room = await NewRoomAsync(facilities, true);
        var open = await NewRoomAsync(facilities, false);

        var pending = await BookAsync(ana, room.Id, Tuesday);
        var confirmedByFacilities = await BookAsync(facilities, room.Id, Tuesday.AddHours(3));
        var confirmedElsewhere = await BookAsync(ana, open.Id, Tuesday);

        Assert.Equal(HttpStatusCode.Created, pending.StatusCode);
        Assert.Equal(BookingStatus.Pending, (await pending.Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options))!.Status);
        Assert.Equal(BookingStatus.Confirmed, (await confirmedByFacilities.Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options))!.Status);
        Assert.Equal(BookingStatus.Confirmed, (await confirmedElsewhere.Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options))!.Status);
    }

    [Fact]
    public async Task A_pending_booking_holds_the_room_and_can_be_cancelled()
    {
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        using var ana = api.ClientFor(Unique("ana"), Roles.Employee);
        using var luis = api.ClientFor(Unique("luis"), Roles.Employee);
        var room = await NewRoomAsync(facilities, true);
        var pending = (await (await BookAsync(ana, room.Id, Tuesday)).Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options))!;
        var url = $"/api/v1/rooms/availability?from={Uri.EscapeDataString(Tuesday.ToString("O"))}&to={Uri.EscapeDataString(Tuesday.AddHours(1).ToString("O"))}&attendees=2";

        Assert.Equal(HttpStatusCode.Conflict, (await BookAsync(luis, room.Id, Tuesday)).StatusCode);
        Assert.DoesNotContain(room.Id, (await luis.GetFromJsonAsync<List<RoomResponse>>(url, TestJson.Options))!.Select(item => item.Id));

        Assert.Equal(HttpStatusCode.NoContent, (await ana.PostAsync($"/api/v1/bookings/{pending.Id}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await BookAsync(luis, room.Id, Tuesday)).StatusCode);
    }
}