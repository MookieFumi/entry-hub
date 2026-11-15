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
/// Check-in: who may do it, from when, and that the first moment is kept.
/// </summary>
[Trait("Category", "Integration")]
public class BookingCheckInTests
{
    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static BookingPolicy Policy => TestPolicy.Default();

    [Fact]
    public void Check_in_opens_fifteen_minutes_before_the_start_and_closes_at_the_end()
    {
        var start = TestClock.MondayMorning;
        var end = start.AddHours(1);

        Assert.Equal(BookingRuleCodes.TooEarlyToCheckIn, Policy.EvaluateCheckIn(BookingStatus.Confirmed, start, end, start.AddMinutes(-16))!.Code);
        Assert.Null(Policy.EvaluateCheckIn(BookingStatus.Confirmed, start, end, start.AddMinutes(-15)));
        Assert.Null(Policy.EvaluateCheckIn(BookingStatus.Confirmed, start, end, start.AddMinutes(59)));
        Assert.Equal(BookingRuleCodes.NotActive, Policy.EvaluateCheckIn(BookingStatus.Confirmed, start, end, end)!.Code);
        Assert.Equal(BookingRuleCodes.NotActive, Policy.EvaluateCheckIn(BookingStatus.Cancelled, start, end, start)!.Code);
    }

    [Fact]
    public async Task Only_the_organizer_or_facilities_check_in_and_not_before_it_opens()
    {
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        using var ana = api.ClientFor(Unique("ana"), Roles.Employee);
        using var luis = api.ClientFor(Unique("luis"), Roles.Employee);
        var room = (await (await facilities.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(Unique("Room"), 1, 8, null))).Content.ReadFromJsonAsync<RoomResponse>(TestJson.Options))!;
        var start = TestClock.MondayMorning.AddMinutes(60);
        var created = await ana.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(room.Id, "Planning", start, start.AddHours(1), 4));
        var booking = (await created.Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options))!;

        Assert.Equal(HttpStatusCode.Forbidden, (await luis.PostAsync($"/api/v1/bookings/{booking.Id}/check-in", null)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await ana.PostAsync($"/api/v1/bookings/{booking.Id}/check-in", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ana.PostAsync($"/api/v1/bookings/{Guid.NewGuid()}/check-in", null)).StatusCode);
    }

    [Fact]
    public async Task Checking_in_sets_the_moment_once_and_a_second_check_in_keeps_it()
    {
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        var ana = Unique("ana");
        using var anaClient = api.ClientFor(ana, Roles.Employee);
        var room = (await (await facilities.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(Unique("Room"), 1, 8, null))).Content.ReadFromJsonAsync<RoomResponse>(TestJson.Options))!;

        // The API cannot create a booking that has already started, so the row is written directly.
        var booking = new Booking
        {
            Id = Guid.CreateVersion7(),
            RoomId = room.Id,
            OrganizerId = ana,
            Title = "Started",
            StartAt = TestClock.MondayMorning.AddMinutes(-10),
            EndAt = TestClock.MondayMorning.AddMinutes(50),
            Attendees = 2,
            CreatedAt = TestClock.MondayMorning.AddDays(-1),
        };
        await using (var db = TestDatabase.CreateContext())
        {
            db.Bookings.Add(booking);
            await db.SaveChangesAsync();
        }

        var first = await anaClient.PostAsync($"/api/v1/bookings/{booking.Id}/check-in", null);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var checkedIn = (await first.Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options))!;
        Assert.NotNull(checkedIn.CheckedInAt);

        var second = (await (await facilities.PostAsync($"/api/v1/bookings/{booking.Id}/check-in", null)).Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options))!;
        Assert.Equal(checkedIn.CheckedInAt, second.CheckedInAt);
    }
}