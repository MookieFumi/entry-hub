using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Domain.Rooms;
using EntryHub.Api.Tests.Support;
using EntryHub.Reminders;

using Npgsql;

namespace EntryHub.Api.Tests.Reminders;

/// <summary>
/// The reminders query runs against the real database. Each test uses its own year, so bookings left by other tests
/// (which live in 2026) or by another test of this class never fall inside its window.
/// </summary>
[Trait("Category", "Integration")]
public class ReminderStoreTests
{
    private static readonly TimeSpan LeadTime = TimeSpan.FromMinutes(15);

    private static DateTimeOffset At(int year, int minutes) => new DateTimeOffset(year, 3, 10, 9, 0, 0, TimeSpan.Zero).AddMinutes(minutes);

    private static async Task<Room> NewRoomAsync()
    {
        await TestDatabase.EnsureMigratedAsync();
        await using var db = TestDatabase.CreateContext();
        var room = new Room
        {
            Id = Guid.CreateVersion7(),
            Name = $"Room {Guid.NewGuid():N}",
            Floor = 1,
            Capacity = 8,
            CreatedAt = TestClock.MondayMorning,
        };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();
        return room;
    }

    private static async Task<Booking> NewBookingAsync(Room room, DateTimeOffset start, BookingStatus status = BookingStatus.Confirmed)
    {
        await using var db = TestDatabase.CreateContext();
        var booking = new Booking
        {
            Id = Guid.CreateVersion7(),
            RoomId = room.Id,
            OrganizerId = $"person-{Guid.NewGuid():N}",
            Title = "Planning",
            StartAt = start,
            EndAt = start.AddHours(1),
            Attendees = 4,
            Status = status,
            CreatedAt = TestClock.MondayMorning,
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();
        return booking;
    }

    private static ReminderStore NewStore() => new(NpgsqlDataSource.Create(TestDatabase.ConnectionString));

    [Fact]
    public async Task A_booking_about_to_start_is_returned_once_and_marked()
    {
        var room = await NewRoomAsync();
        var start = At(2031, 10);
        var booking = await NewBookingAsync(room, start);
        var store = NewStore();

        var first = await store.ClaimDueAsync(At(2031, 0), LeadTime, CancellationToken.None);
        var second = await store.ClaimDueAsync(At(2031, 1), LeadTime, CancellationToken.None);

        var reminder = Assert.Single(first);
        Assert.Equal(booking.Id, reminder.BookingId);
        Assert.Equal(booking.OrganizerId, reminder.OrganizerId);
        Assert.Equal(room.Name, reminder.RoomName);
        Assert.Equal(start, reminder.StartAt);
        Assert.Empty(second);

        await using var db = TestDatabase.CreateContext();
        Assert.Equal(At(2031, 0), (await db.Bookings.FindAsync(booking.Id))?.ReminderSentAt);
    }

    [Fact]
    public async Task A_booking_far_in_the_future_waits_until_it_is_inside_the_lead_time()
    {
        var room = await NewRoomAsync();
        var booking = await NewBookingAsync(room, At(2032, 60));
        var store = NewStore();

        Assert.Empty(await store.ClaimDueAsync(At(2032, 0), LeadTime, CancellationToken.None));

        var due = await store.ClaimDueAsync(At(2032, 50), LeadTime, CancellationToken.None);
        Assert.Equal(booking.Id, Assert.Single(due).BookingId);
    }

    [Fact]
    public async Task Cancelled_and_already_started_bookings_are_ignored()
    {
        var room = await NewRoomAsync();
        await NewBookingAsync(room, At(2033, 10), BookingStatus.Cancelled);
        var started = await NewRoomAsync();
        await NewBookingAsync(started, At(2033, -5));

        var due = await NewStore().ClaimDueAsync(At(2033, 0), LeadTime, CancellationToken.None);

        Assert.Empty(due);
    }

    [Fact]
    public async Task Several_copies_of_the_service_never_remind_the_same_booking_twice()
    {
        var bookings = new List<Booking>();
        for (var i = 0; i < 6; i++)
        {
            bookings.Add(await NewBookingAsync(await NewRoomAsync(), At(2034, 10)));
        }

        var claims = await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(_ => NewStore().ClaimDueAsync(At(2034, 0), LeadTime, CancellationToken.None)));

        var ids = claims.SelectMany(claim => claim).Select(reminder => reminder.BookingId).ToList();
        Assert.Equal(bookings.Count, ids.Count);
        Assert.Equal(bookings.Select(booking => booking.Id).Order(), ids.Order());
    }
}