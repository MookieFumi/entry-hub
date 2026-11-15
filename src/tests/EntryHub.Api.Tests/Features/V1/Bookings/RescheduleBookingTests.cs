using System.Net;
using System.Net.Http.Json;

using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Features.V1.Bookings;
using EntryHub.Api.Features.V1.Bookings.CreateBooking;
using EntryHub.Api.Features.V1.Bookings.RescheduleBooking;
using EntryHub.Api.Features.V1.Rooms;
using EntryHub.Api.Features.V1.Rooms.CreateRoom;
using EntryHub.Api.Security;
using EntryHub.Api.Tests.Support;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Tests.Features.V1.Bookings;

/// <summary>
/// Moving a booking to another time or room. The API runs on <see cref="TestClock"/>, Monday 09:00 in the office.
/// </summary>
[Trait("Category", "Integration")]
public class RescheduleBookingTests
{
    // Tuesday 2026-10-20 and Wednesday 2026-10-21, 10:00 in Madrid.
    private static readonly DateTimeOffset Tuesday = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Wednesday = Tuesday.AddDays(1);

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static async Task<RoomResponse> NewRoomAsync(TestApi api, int capacity = 8)
    {
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        var response = await facilities.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(Unique("Room"), 1, capacity, null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RoomResponse>(TestJson.Options))!;
    }

    private static async Task<BookingResponse> BookAsync(HttpClient client, Guid roomId, DateTimeOffset start, int attendees = 4)
    {
        var response = await client.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(roomId, "Planning", start, start.AddHours(1), attendees));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options))!;
    }

    private static Task<HttpResponseMessage> MoveAsync(HttpClient client, Guid bookingId, DateTimeOffset start, Guid? roomId = null, TimeSpan? length = null) =>
        client.PostAsJsonAsync($"/api/v1/bookings/{bookingId}/reschedule", new RescheduleBookingRequest(start, start + (length ?? TimeSpan.FromHours(1)), roomId));

    private static async Task<string[]> CodesAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<RulesBrokenProblem>(TestJson.Options);
        return [.. problem!.Violations.Select(violation => violation.Code)];
    }

    [Fact]
    public async Task The_organizer_moves_the_booking_and_it_keeps_its_id()
    {
        using var api = await TestApi.StartAsync();
        var room = await NewRoomAsync(api);
        using var ana = api.ClientFor(Unique("ana"), Roles.Employee);
        var booking = await BookAsync(ana, room.Id, Tuesday);

        var response = await MoveAsync(ana, booking.Id, Wednesday);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var moved = await response.Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options);
        Assert.Equal((booking.Id, booking.Title, booking.OrganizerId), (moved!.Id, moved.Title, moved.OrganizerId));
        Assert.Equal((Wednesday, Wednesday.AddHours(1)), (moved.StartAt, moved.EndAt));
        Assert.NotNull(moved.RescheduledAt);

        // The old time is free again and the new one is taken.
        using var bea = api.ClientFor(Unique("bea"), Roles.Employee);
        Assert.Equal(HttpStatusCode.Created, (await bea.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(room.Id, "Other", Tuesday, Tuesday.AddHours(1), 2))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await bea.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(room.Id, "Other", Wednesday, Wednesday.AddHours(1), 2))).StatusCode);
    }

    [Fact]
    public async Task A_booking_can_move_to_another_room_and_facilities_can_move_anyones()
    {
        using var api = await TestApi.StartAsync();
        var first = await NewRoomAsync(api);
        var second = await NewRoomAsync(api);
        using var ana = api.ClientFor(Unique("ana"), Roles.Employee);
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        var booking = await BookAsync(ana, first.Id, Tuesday);

        var response = await MoveAsync(facilities, booking.Id, Tuesday, second.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(second.Id, (await response.Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options))!.RoomId);
    }

    [Fact]
    public async Task Someone_else_gets_403_and_an_unknown_booking_404()
    {
        using var api = await TestApi.StartAsync();
        var room = await NewRoomAsync(api);
        using var ana = api.ClientFor(Unique("ana"), Roles.Employee);
        using var bea = api.ClientFor(Unique("bea"), Roles.Employee);
        var booking = await BookAsync(ana, room.Id, Tuesday);

        Assert.Equal(HttpStatusCode.Forbidden, (await MoveAsync(bea, booking.Id, Wednesday)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await MoveAsync(ana, Guid.CreateVersion7(), Wednesday)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ana.PostAsJsonAsync($"/api/v1/bookings/{booking.Id}/reschedule", new RescheduleBookingRequest(Wednesday, Wednesday.AddHours(-1)))).StatusCode);
    }

    [Fact]
    public async Task The_new_time_has_to_pass_the_same_rules_as_a_new_booking()
    {
        using var api = await TestApi.StartAsync();
        var small = await NewRoomAsync(api, capacity: 4);
        var room = await NewRoomAsync(api);
        using var ana = api.ClientFor(Unique("ana"), Roles.Employee);
        var booking = await BookAsync(ana, room.Id, Tuesday, attendees: 6);

        var night = await MoveAsync(ana, booking.Id, Tuesday.AddHours(12));
        var tooLong = await MoveAsync(ana, booking.Id, Wednesday, length: TimeSpan.FromHours(5));
        var tooSmall = await MoveAsync(ana, booking.Id, Wednesday, small.Id);
        var missing = await MoveAsync(ana, booking.Id, Wednesday, Guid.CreateVersion7());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, night.StatusCode);
        Assert.Contains(BookingRuleCodes.OutsideOfficeHours, await CodesAsync(night));
        Assert.Contains(BookingRuleCodes.DurationTooLong, await CodesAsync(tooLong));
        Assert.Contains(BookingRuleCodes.ExceedsCapacity, await CodesAsync(tooSmall));
        Assert.Contains(BookingRuleCodes.RoomUnavailable, await CodesAsync(missing));

        var unchanged = await ana.GetFromJsonAsync<BookingResponse>($"/api/v1/bookings/{booking.Id}", TestJson.Options);
        Assert.Equal((Tuesday, null), (unchanged!.StartAt, unchanged.RescheduledAt));
    }

    [Fact]
    public async Task A_slot_taken_by_another_booking_is_a_conflict_but_overlapping_the_own_old_time_is_not()
    {
        using var api = await TestApi.StartAsync();
        var room = await NewRoomAsync(api);
        using var ana = api.ClientFor(Unique("ana"), Roles.Employee);
        using var bea = api.ClientFor(Unique("bea"), Roles.Employee);
        var mine = await BookAsync(ana, room.Id, Tuesday);
        await BookAsync(bea, room.Id, Wednesday);

        Assert.Equal(HttpStatusCode.Conflict, (await MoveAsync(ana, mine.Id, Wednesday.AddMinutes(30))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await MoveAsync(ana, mine.Id, Tuesday.AddMinutes(30))).StatusCode);
    }

    [Fact]
    public async Task Two_bookings_racing_for_the_same_slot_leave_exactly_one_winner()
    {
        using var api = await TestApi.StartAsync();
        var room = await NewRoomAsync(api);
        var clients = Enumerable.Range(0, 6).Select(_ => api.ClientFor(Unique("ana"), Roles.Employee)).ToList();
        try
        {
            var bookings = new List<BookingResponse>();
            for (var index = 0; index < clients.Count; index++)
            {
                bookings.Add(await BookAsync(clients[index], room.Id, Tuesday.AddHours(index + 1)));
            }

            var results = await Task.WhenAll(clients.Select((client, index) => MoveAsync(client, bookings[index].Id, Wednesday)));

            Assert.Equal(1, results.Count(result => result.StatusCode == HttpStatusCode.OK));
            Assert.Equal(clients.Count - 1, results.Count(result => result.StatusCode == HttpStatusCode.Conflict));
        }
        finally
        {
            clients.ForEach(client => client.Dispose());
        }
    }

    [Fact]
    public async Task A_cancelled_booking_cannot_be_moved()
    {
        using var api = await TestApi.StartAsync();
        var room = await NewRoomAsync(api);
        using var ana = api.ClientFor(Unique("ana"), Roles.Employee);
        var booking = await BookAsync(ana, room.Id, Tuesday);
        Assert.Equal(HttpStatusCode.NoContent, (await ana.PostAsync($"/api/v1/bookings/{booking.Id}/cancel", null)).StatusCode);

        var response = await MoveAsync(ana, booking.Id, Wednesday);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains(BookingRuleCodes.NotActive, await CodesAsync(response));
    }

    [Fact]
    public async Task An_employee_cannot_move_a_booking_that_starts_inside_the_notice_period_but_facilities_can()
    {
        using var api = await TestApi.StartAsync();
        var room = await NewRoomAsync(api);
        var organizer = Unique("ana");
        // Starts ten minutes after the clock of the API (09:00 in the office): too late for an Employee.
        var soon = TestClock.MondayMorning.AddMinutes(10);
        var bookingId = await InsertBookingAsync(room.Id, organizer, soon, reminderSentAt: null);
        using var ana = api.ClientFor(organizer, Roles.Employee);
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);

        var refused = await MoveAsync(ana, bookingId, Wednesday);
        var allowed = await MoveAsync(facilities, bookingId, Wednesday);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Contains(BookingRuleCodes.TooLateToReschedule, await CodesAsync(refused));
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task Moving_to_a_later_time_asks_for_a_new_reminder_and_moving_earlier_does_not()
    {
        using var api = await TestApi.StartAsync();
        var room = await NewRoomAsync(api);
        var organizer = Unique("ana");
        var reminded = TestClock.MondayMorning.AddDays(1);
        var laterId = await InsertBookingAsync(room.Id, organizer, Tuesday, reminderSentAt: reminded);
        var earlierId = await InsertBookingAsync(room.Id, organizer, Wednesday.AddHours(3), reminderSentAt: reminded);
        using var ana = api.ClientFor(organizer, Roles.Employee);

        Assert.Equal(HttpStatusCode.OK, (await MoveAsync(ana, laterId, Tuesday.AddHours(2))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await MoveAsync(ana, earlierId, Wednesday.AddHours(1))).StatusCode);

        Assert.Null(await ReminderSentAtAsync(laterId));
        Assert.NotNull(await ReminderSentAtAsync(earlierId));
    }

    private static async Task<Guid> InsertBookingAsync(Guid roomId, string organizer, DateTimeOffset start, DateTimeOffset? reminderSentAt)
    {
        await using var db = TestDatabase.CreateContext();
        var booking = new Booking
        {
            Id = Guid.CreateVersion7(),
            RoomId = roomId,
            OrganizerId = organizer,
            Title = "Planning",
            StartAt = start,
            EndAt = start.AddHours(1),
            Attendees = 4,
            CreatedAt = TestClock.MondayMorning,
            ReminderSentAt = reminderSentAt,
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();
        return booking.Id;
    }

    private static async Task<DateTimeOffset?> ReminderSentAtAsync(Guid bookingId)
    {
        await using var db = TestDatabase.CreateContext();
        return await db.Bookings.AsNoTracking().Where(booking => booking.Id == bookingId).Select(booking => booking.ReminderSentAt).SingleAsync();
    }
}