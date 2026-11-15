using System.Net;
using System.Net.Http.Json;

using EntryHub.Api.Features.V1.Bookings;
using EntryHub.Api.Features.V1.Bookings.CreateBooking;
using EntryHub.Api.Features.V1.Bookings.RescheduleBooking;
using EntryHub.Api.Features.V1.Rooms;
using EntryHub.Api.Features.V1.Rooms.CreateRoom;
using EntryHub.Api.Security;
using EntryHub.Api.Tests.Support;

namespace EntryHub.Api.Tests.Features.V1;

/// <summary>
/// Buffers of a room: minutes kept free around each booking, enforced by the database and by the availability search.
/// </summary>
[Trait("Category", "Integration")]
public class RoomBuffersTests
{
    private static readonly DateTimeOffset Tuesday = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static async Task<RoomResponse> NewRoomAsync(HttpClient facilities, int before, int after)
    {
        var response = await facilities.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(Unique("Room"), 1, 8, null, BufferBeforeMinutes: before, BufferAfterMinutes: after));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RoomResponse>(TestJson.Options))!;
    }

    private static CreateBookingRequest Booking(Guid roomId, DateTimeOffset start, int minutes = 60) => new(roomId, "Planning", start, start.AddMinutes(minutes), 4);

    private static async Task<HttpStatusCode> BookAsync(HttpClient client, Guid roomId, DateTimeOffset start)
        => (await client.PostAsJsonAsync("/api/v1/bookings", Booking(roomId, start))).StatusCode;

    [Fact]
    public async Task A_room_has_no_buffer_unless_one_is_set_and_the_buffer_can_be_changed()
    {
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);

        var plain = await facilities.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(Unique("Room"), 1, 8, null));
        var room = (await plain.Content.ReadFromJsonAsync<RoomResponse>(TestJson.Options))!;
        var updated = await facilities.PatchAsJsonAsync($"/api/v1/rooms/{room.Id}", new { bufferBeforeMinutes = 10, bufferAfterMinutes = 15 });
        var tooLong = await facilities.PatchAsJsonAsync($"/api/v1/rooms/{room.Id}", new { bufferAfterMinutes = 241 });

        Assert.Equal((0, 0), (room.BufferBeforeMinutes, room.BufferAfterMinutes));
        var after = (await updated.Content.ReadFromJsonAsync<RoomResponse>(TestJson.Options))!;
        Assert.Equal((10, 15), (after.BufferBeforeMinutes, after.BufferAfterMinutes));
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
    }

    [Fact]
    public async Task A_booking_cannot_start_inside_the_buffer_after_another_one()
    {
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        using var ana = api.ClientFor(Unique("ana"), Roles.Employee);
        using var luis = api.ClientFor(Unique("luis"), Roles.Employee);
        var room = await NewRoomAsync(facilities, 0, 30);

        Assert.Equal(HttpStatusCode.Created, await BookAsync(ana, room.Id, Tuesday));

        // The first booking ends at 09:00 UTC and the room stays free until 09:30.
        Assert.Equal(HttpStatusCode.Conflict, await BookAsync(luis, room.Id, Tuesday.AddMinutes(75)));
        Assert.Equal(HttpStatusCode.Created, await BookAsync(luis, room.Id, Tuesday.AddMinutes(90)));
    }

    [Fact]
    public async Task A_booking_cannot_end_inside_the_buffer_before_another_one()
    {
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        using var ana = api.ClientFor(Unique("ana"), Roles.Employee);
        using var luis = api.ClientFor(Unique("luis"), Roles.Employee);
        var room = await NewRoomAsync(facilities, 20, 0);

        Assert.Equal(HttpStatusCode.Created, await BookAsync(ana, room.Id, Tuesday.AddHours(2)));

        // The first booking starts at 10:00 UTC and the room is kept free from 09:40.
        Assert.Equal(HttpStatusCode.Conflict, await BookAsync(luis, room.Id, Tuesday.AddMinutes(60)));
        Assert.Equal(HttpStatusCode.Created, await BookAsync(luis, room.Id, Tuesday));
    }

    [Fact]
    public async Task Changing_the_buffer_does_not_change_bookings_that_already_exist()
    {
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        using var ana = api.ClientFor(Unique("ana"), Roles.Employee);
        using var luis = api.ClientFor(Unique("luis"), Roles.Employee);
        var room = await NewRoomAsync(facilities, 0, 0);

        Assert.Equal(HttpStatusCode.Created, await BookAsync(ana, room.Id, Tuesday));
        (await facilities.PatchAsJsonAsync($"/api/v1/rooms/{room.Id}", new { bufferAfterMinutes = 30 })).EnsureSuccessStatusCode();

        // The existing booking kept its zero buffer; the new one carries the 30 minutes after itself.
        Assert.Equal(HttpStatusCode.Created, await BookAsync(luis, room.Id, Tuesday.AddMinutes(60)));
        Assert.Equal(HttpStatusCode.Conflict, await BookAsync(ana, room.Id, Tuesday.AddMinutes(120)));
        Assert.Equal(HttpStatusCode.Created, await BookAsync(ana, room.Id, Tuesday.AddMinutes(150)));
    }

    [Fact]
    public async Task Rescheduling_keeps_the_buffer_and_takes_the_one_of_the_new_room()
    {
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        using var ana = api.ClientFor(Unique("ana"), Roles.Employee);
        using var luis = api.ClientFor(Unique("luis"), Roles.Employee);
        var room = await NewRoomAsync(facilities, 0, 30);
        var created = await ana.PostAsJsonAsync("/api/v1/bookings", Booking(room.Id, Tuesday));
        var booking = (await created.Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options))!;
        Assert.Equal(HttpStatusCode.Created, await BookAsync(luis, room.Id, Tuesday.AddHours(3)));

        // Ending at 10:45 leaves 15 minutes before the booking at 11:00, ending at 10:30 leaves 30.
        var tooClose = await ana.PostAsJsonAsync($"/api/v1/bookings/{booking.Id}/reschedule", new RescheduleBookingRequest(Tuesday.AddMinutes(105), Tuesday.AddMinutes(165), null));
        var fits = await ana.PostAsJsonAsync($"/api/v1/bookings/{booking.Id}/reschedule", new RescheduleBookingRequest(Tuesday.AddMinutes(90), Tuesday.AddMinutes(150), null));

        Assert.Equal(HttpStatusCode.Conflict, tooClose.StatusCode);
        Assert.Equal(HttpStatusCode.OK, fits.StatusCode);
    }

    [Fact]
    public async Task The_availability_search_counts_the_buffers_of_the_room_and_of_the_booking()
    {
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        using var ana = api.ClientFor(Unique("ana"), Roles.Employee);
        var buffered = await NewRoomAsync(facilities, 0, 15);
        var plain = await NewRoomAsync(facilities, 0, 0);
        Assert.Equal(HttpStatusCode.Created, await BookAsync(ana, buffered.Id, Tuesday));
        Assert.Equal(HttpStatusCode.Created, await BookAsync(ana, plain.Id, Tuesday));

        async Task<List<Guid>> FreeAsync(DateTimeOffset from)
        {
            var url = $"/api/v1/rooms/availability?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(from.AddMinutes(30).ToString("O"))}&attendees=2";
            return (await ana.GetFromJsonAsync<List<RoomResponse>>(url, TestJson.Options))!.Select(room => room.Id).ToList();
        }

        // 09:00-09:30 is free in the plain room but inside the buffer of the other; from 09:15 both are free.
        var atNine = await FreeAsync(Tuesday.AddHours(1));
        var atQuarterPast = await FreeAsync(Tuesday.AddMinutes(75));
        Assert.Contains(plain.Id, atNine);
        Assert.DoesNotContain(buffered.Id, atNine);
        Assert.Contains(buffered.Id, atQuarterPast);
        Assert.Contains(plain.Id, atQuarterPast);
    }
}