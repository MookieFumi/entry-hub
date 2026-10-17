using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Domain.Rooms;
using EntryHub.Api.Features.V1.Bookings;
using EntryHub.Api.Features.V1.Bookings.CancelBooking;
using EntryHub.Api.Features.V1.Bookings.CreateBooking;
using EntryHub.Api.Features.V1.Bookings.GetBooking;
using EntryHub.Api.Features.V1.Bookings.ListMyBookings;
using EntryHub.Api.Features.V1.Common;
using EntryHub.Api.Tests.Support;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Tests.Features.V1.Bookings;

/// <summary>
/// Layer 2: handlers without HTTP against a real PostgreSQL. Every test builds its own room and caller, so
/// tests can run in parallel on one database and leave no state that matters to the next run.
/// </summary>
[Trait("Category", "Integration")]
public class BookingsHandlerTests
{
    private static readonly DateTimeOffset Tuesday = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero); // 10:00 local

    private static Caller NewEmployee() => new($"employee-{Guid.NewGuid():N}", IsFacilities: false);

    private static Caller NewFacilities() => new($"facilities-{Guid.NewGuid():N}", IsFacilities: true);

    private static async Task<Room> NewRoomAsync(int capacity = 8, bool active = true)
    {
        await TestDatabase.EnsureMigratedAsync();
        await using var db = TestDatabase.CreateContext();
        var room = new Room
        {
            Id = Guid.CreateVersion7(),
            Name = $"Room {Guid.NewGuid():N}",
            Floor = 1,
            Capacity = capacity,
            IsActive = active,
            CreatedAt = TestClock.MondayMorning,
        };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();
        return room;
    }

    private static async Task<CreateBookingResult> BookAsync(
        Caller caller,
        Guid roomId,
        DateTimeOffset start,
        TimeSpan? length = null,
        int attendees = 4,
        TimeProvider? clock = null)
    {
        await using var db = TestDatabase.CreateContext();
        var handler = new CreateBookingHandler(db, TestPolicy.Default(), clock ?? TestClock.Default);

        return await handler.HandleAsync(
            caller,
            new CreateBookingRequest(roomId, "Planning", start, start + (length ?? TimeSpan.FromHours(1)), attendees),
            CancellationToken.None);
    }

    private static async Task<CancelBookingResult> CancelAsync(Caller caller, Guid bookingId, TimeProvider? clock = null)
    {
        await using var db = TestDatabase.CreateContext();

        return await new CancelBookingHandler(db, TestPolicy.Default(), clock ?? TestClock.Default)
            .HandleAsync(caller, bookingId, CancellationToken.None);
    }

    private static BookingResponse Created(CreateBookingResult result) =>
        Assert.IsType<CreateBookingResult.Created>(result).Booking;

    [Fact]
    public async Task A_valid_booking_is_stored_for_the_caller()
    {
        var room = await NewRoomAsync();
        var ana = NewEmployee();

        var booking = Created(await BookAsync(ana, room.Id, Tuesday));

        Assert.Equal(ana.Id, booking.OrganizerId);
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.Equal(Tuesday, booking.StartAt);

        await using var db = TestDatabase.CreateContext();
        var found = Assert.IsType<GetBookingResult.Found>(await new GetBookingHandler(db).HandleAsync(ana, booking.Id, CancellationToken.None));
        Assert.Equal(booking.Id, found.Booking.Id);
    }

    [Fact]
    public async Task A_missing_or_inactive_room_is_a_rule_violation()
    {
        var inactive = await NewRoomAsync(active: false);

        foreach (var roomId in new[] { Guid.CreateVersion7(), inactive.Id })
        {
            var result = await BookAsync(NewEmployee(), roomId, Tuesday);

            var broken = Assert.IsType<CreateBookingResult.RulesBroken>(result);
            Assert.Equal(BookingRuleCodes.RoomUnavailable, Assert.Single(broken.Violations).Code);
        }
    }

    [Fact]
    public async Task Broken_rules_are_reported_and_nothing_is_stored()
    {
        var room = await NewRoomAsync(capacity: 4);
        var ana = NewEmployee();

        var result = await BookAsync(ana, room.Id, Tuesday, TimeSpan.FromHours(5), attendees: 10);

        var broken = Assert.IsType<CreateBookingResult.RulesBroken>(result);
        Assert.Equal(
            [BookingRuleCodes.DurationTooLong, BookingRuleCodes.ExceedsCapacity],
            broken.Violations.Select(violation => violation.Code));

        await using var db = TestDatabase.CreateContext();
        Assert.Empty(await db.Bookings.Where(booking => booking.OrganizerId == ana.Id).ToListAsync());
    }

    [Fact]
    public async Task An_overlapping_booking_of_the_same_room_is_refused()
    {
        var room = await NewRoomAsync();
        Created(await BookAsync(NewEmployee(), room.Id, Tuesday));

        Assert.IsType<CreateBookingResult.Overlaps>(await BookAsync(NewEmployee(), room.Id, Tuesday.AddMinutes(30)));
        Assert.IsType<CreateBookingResult.Overlaps>(await BookAsync(NewEmployee(), room.Id, Tuesday, TimeSpan.FromMinutes(15)));
    }

    [Fact]
    public async Task Back_to_back_bookings_and_other_rooms_do_not_overlap()
    {
        var room = await NewRoomAsync();
        var other = await NewRoomAsync();
        Created(await BookAsync(NewEmployee(), room.Id, Tuesday));

        Created(await BookAsync(NewEmployee(), room.Id, Tuesday.AddHours(1)));
        Created(await BookAsync(NewEmployee(), room.Id, Tuesday.AddHours(-1)));
        Created(await BookAsync(NewEmployee(), other.Id, Tuesday));
    }

    [Fact]
    public async Task A_cancelled_booking_frees_the_slot()
    {
        var room = await NewRoomAsync();
        var ana = NewEmployee();
        var booking = Created(await BookAsync(ana, room.Id, Tuesday));

        Assert.IsType<CancelBookingResult.Cancelled>(await CancelAsync(ana, booking.Id));

        Created(await BookAsync(NewEmployee(), room.Id, Tuesday));
    }

    [Fact]
    public async Task Simultaneous_requests_for_one_slot_have_exactly_one_winner()
    {
        var room = await NewRoomAsync();

        var results = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => BookAsync(NewEmployee(), room.Id, Tuesday)));

        Assert.Single(results.OfType<CreateBookingResult.Created>());
        Assert.Equal(7, results.OfType<CreateBookingResult.Overlaps>().Count());
    }

    [Fact]
    public async Task An_employee_is_limited_to_five_active_bookings_and_facilities_is_not()
    {
        var room = await NewRoomAsync();
        var ana = NewEmployee();
        var fran = NewFacilities();

        for (var hour = 0; hour < 5; hour++)
        {
            Created(await BookAsync(ana, room.Id, Tuesday.AddHours(hour)));
        }

        var sixth = await BookAsync(ana, room.Id, Tuesday.AddHours(5));
        var broken = Assert.IsType<CreateBookingResult.RulesBroken>(sixth);
        Assert.Equal(BookingRuleCodes.TooManyActiveBookings, Assert.Single(broken.Violations).Code);

        for (var hour = 0; hour < 6; hour++)
        {
            Created(await BookAsync(fran, room.Id, Tuesday.AddDays(1).AddHours(hour)));
        }
    }

    [Fact]
    public async Task Cancelling_gives_back_one_of_the_five_places()
    {
        var room = await NewRoomAsync();
        var ana = NewEmployee();
        var first = Created(await BookAsync(ana, room.Id, Tuesday));
        for (var hour = 1; hour < 5; hour++)
        {
            Created(await BookAsync(ana, room.Id, Tuesday.AddHours(hour)));
        }

        await CancelAsync(ana, first.Id);

        Created(await BookAsync(ana, room.Id, Tuesday.AddHours(5)));
    }

    [Fact]
    public async Task Only_the_owner_or_facilities_can_cancel()
    {
        var room = await NewRoomAsync();
        var ana = NewEmployee();
        var booking = Created(await BookAsync(ana, room.Id, Tuesday));

        Assert.IsType<CancelBookingResult.Forbidden>(await CancelAsync(NewEmployee(), booking.Id));
        Assert.IsType<CancelBookingResult.Cancelled>(await CancelAsync(NewFacilities(), booking.Id));
    }

    [Fact]
    public async Task Cancelling_twice_or_an_unknown_booking_behaves_predictably()
    {
        var room = await NewRoomAsync();
        var ana = NewEmployee();
        var booking = Created(await BookAsync(ana, room.Id, Tuesday));

        Assert.IsType<CancelBookingResult.Cancelled>(await CancelAsync(ana, booking.Id));
        Assert.IsType<CancelBookingResult.Cancelled>(await CancelAsync(ana, booking.Id));
        Assert.IsType<CancelBookingResult.NotFound>(await CancelAsync(ana, Guid.CreateVersion7()));
    }

    [Fact]
    public async Task An_employee_cannot_cancel_inside_the_notice_period_but_facilities_can()
    {
        var room = await NewRoomAsync();
        var ana = NewEmployee();
        var booking = Created(await BookAsync(ana, room.Id, Tuesday));
        var tenMinutesBefore = new TestClock(Tuesday.AddMinutes(-10));

        var late = await CancelAsync(ana, booking.Id, tenMinutesBefore);

        Assert.Equal(BookingRuleCodes.TooLateToCancel, Assert.IsType<CancelBookingResult.TooLate>(late).Violation.Code);
        Assert.IsType<CancelBookingResult.Cancelled>(await CancelAsync(NewFacilities(), booking.Id, tenMinutesBefore));
    }

    [Fact]
    public async Task Get_is_limited_to_the_owner_and_facilities()
    {
        var room = await NewRoomAsync();
        var ana = NewEmployee();
        var booking = Created(await BookAsync(ana, room.Id, Tuesday));
        await using var db = TestDatabase.CreateContext();
        var handler = new GetBookingHandler(db);

        Assert.IsType<GetBookingResult.Forbidden>(await handler.HandleAsync(NewEmployee(), booking.Id, CancellationToken.None));
        Assert.IsType<GetBookingResult.Found>(await handler.HandleAsync(NewFacilities(), booking.Id, CancellationToken.None));
        Assert.IsType<GetBookingResult.NotFound>(await handler.HandleAsync(ana, Guid.CreateVersion7(), CancellationToken.None));
    }

    [Fact]
    public async Task My_bookings_only_lists_my_own_and_hides_cancelled_ones_by_default()
    {
        var room = await NewRoomAsync();
        var ana = NewEmployee();
        var bea = NewEmployee();
        var kept = Created(await BookAsync(ana, room.Id, Tuesday));
        var cancelled = Created(await BookAsync(ana, room.Id, Tuesday.AddHours(1)));
        Created(await BookAsync(bea, room.Id, Tuesday.AddHours(2)));
        await CancelAsync(ana, cancelled.Id);
        await using var db = TestDatabase.CreateContext();
        var handler = new ListMyBookingsHandler(db);

        var visible = await handler.HandleAsync(ana, new ListMyBookingsQuery(null, null, null), CancellationToken.None);
        var all = await handler.HandleAsync(ana, new ListMyBookingsQuery(true, null, 1), CancellationToken.None);

        Assert.Equal([kept.Id], visible.Items.Select(booking => booking.Id));
        Assert.Equal(2, all.TotalCount);
        Assert.Single(all.Items);
        Assert.Equal(cancelled.Id, all.Items[0].Id); // newest start first
    }
}