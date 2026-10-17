using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Domain.Rooms;
using EntryHub.Api.Features.V1.Rooms.CreateRoom;
using EntryHub.Api.Features.V1.Rooms.GetRoom;
using EntryHub.Api.Features.V1.Rooms.ListRooms;
using EntryHub.Api.Features.V1.Rooms.StreamRoomOccupancy;
using EntryHub.Api.Tests.Support;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EntryHub.Api.Tests.Features.V1.Rooms;

/// <summary>
/// Layer 2: handlers run without HTTP against a real PostgreSQL.
/// Every test uses names and floors nobody else uses, so tests can run in parallel on one database.
/// </summary>
[Trait("Category", "Integration")]
public class RoomsHandlerTests
{
    private static string UniqueName() => $"Room {Guid.NewGuid():N}";

    // Floors reserved for tests that count rooms; each one is emptied first because the database outlives the run.
    private const int HandlerListFloor = 90;

    [Fact]
    public async Task Create_stores_the_room_with_the_clock_time_and_clean_equipment()
    {
        await TestDatabase.EnsureMigratedAsync();
        await using var db = TestDatabase.CreateContext();
        var now = new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);
        var handler = new CreateRoomHandler(db, new FixedClock(now));
        var name = UniqueName();

        var created = await handler.HandleAsync(
            new CreateRoomRequest($"  {name}  ", 3, 8, [" Projector ", "", "Projector", "Whiteboard"]),
            CancellationToken.None);

        Assert.NotNull(created);
        Assert.Equal(name, created.Name);
        Assert.Equal(now, created.CreatedAt);
        Assert.Equal(["Projector", "Whiteboard"], created.Equipment);

        await using var verify = TestDatabase.CreateContext();
        var stored = await new GetRoomHandler(verify).HandleAsync(created.Id, CancellationToken.None);
        Assert.Equivalent(created, stored);
    }

    [Fact]
    public async Task Create_returns_null_when_the_name_is_taken()
    {
        await TestDatabase.EnsureMigratedAsync();
        await using var db = TestDatabase.CreateContext();
        var handler = new CreateRoomHandler(db, TimeProvider.System);
        var request = new CreateRoomRequest(UniqueName(), 1, 4, null);

        Assert.NotNull(await handler.HandleAsync(request, CancellationToken.None));
        await using var second = TestDatabase.CreateContext();
        var duplicate = await new CreateRoomHandler(second, TimeProvider.System).HandleAsync(request, CancellationToken.None);

        Assert.Null(duplicate);
    }

    [Fact]
    public async Task Create_returns_null_when_the_unique_index_wins_a_race()
    {
        await TestDatabase.EnsureMigratedAsync();
        var request = new CreateRoomRequest(UniqueName(), 1, 4, null);

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var db = TestDatabase.CreateContext();
            return await new CreateRoomHandler(db, TimeProvider.System).HandleAsync(request, CancellationToken.None);
        }));

        Assert.Equal(1, results.Count(room => room is not null));
    }

    [Fact]
    public async Task Get_returns_null_for_an_unknown_room()
    {
        await TestDatabase.EnsureMigratedAsync();
        await using var db = TestDatabase.CreateContext();

        Assert.Null(await new GetRoomHandler(db).HandleAsync(Guid.CreateVersion7(), CancellationToken.None));
    }

    [Fact]
    public async Task List_filters_by_floor_and_capacity_and_pages_the_result()
    {
        await TestDatabase.EnsureMigratedAsync();
        await using var db = TestDatabase.CreateContext();
        const int floor = HandlerListFloor;
        await db.Bookings.Where(booking => db.Rooms.Any(room => room.Id == booking.RoomId && room.Floor == floor)).ExecuteDeleteAsync();
        await db.Rooms.Where(room => room.Floor == floor).ExecuteDeleteAsync();
        var create = new CreateRoomHandler(db, TimeProvider.System);
        foreach (var capacity in new[] { 4, 6, 10, 12 })
        {
            await create.HandleAsync(new CreateRoomRequest(UniqueName(), floor, capacity, null), CancellationToken.None);
        }

        var list = new ListRoomsHandler(db);
        var all = await list.HandleAsync(new ListRoomsQuery(floor, null, null, null), CancellationToken.None);
        var big = await list.HandleAsync(new ListRoomsQuery(floor, 8, null, null), CancellationToken.None);
        var secondPage = await list.HandleAsync(new ListRoomsQuery(floor, null, 2, 3), CancellationToken.None);

        Assert.Equal(4, all.TotalCount);
        Assert.Equal(2, big.TotalCount);
        Assert.All(big.Items, room => Assert.True(room.Capacity >= 8));
        Assert.Equal(4, secondPage.TotalCount);
        Assert.Single(secondPage.Items);
        Assert.Equal(2, secondPage.Page);
    }

    [Fact]
    public async Task Stream_is_null_for_an_unknown_room_and_emits_snapshots_for_a_known_one()
    {
        await TestDatabase.EnsureMigratedAsync();
        await using var db = TestDatabase.CreateContext();
        var room = await new CreateRoomHandler(db, TimeProvider.System)
            .HandleAsync(new CreateRoomRequest(UniqueName(), 1, 4, null), CancellationToken.None);
        var options = Options.Create(new StreamRoomOccupancyOptions { Interval = TimeSpan.FromMilliseconds(10) });
        var handler = new StreamRoomOccupancyHandler(db, TimeProvider.System, options);

        Assert.Null(await handler.HandleAsync(Guid.CreateVersion7(), CancellationToken.None));

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var stream = await handler.HandleAsync(room!.Id, cancellation.Token);
        var snapshots = new List<RoomOccupancy>();
        await foreach (var snapshot in stream!.WithCancellation(cancellation.Token))
        {
            snapshots.Add(snapshot);
            if (snapshots.Count == 3)
            {
                break;
            }
        }

        Assert.Equal(3, snapshots.Count);
        Assert.All(snapshots, snapshot => Assert.Equal(room.Id, snapshot.RoomId));
    }

    [Fact]
    public async Task Stream_reports_the_room_as_occupied_while_a_confirmed_booking_runs()
    {
        await TestDatabase.EnsureMigratedAsync();
        await using var db = TestDatabase.CreateContext();
        var room = await new CreateRoomHandler(db, TimeProvider.System)
            .HandleAsync(new CreateRoomRequest(UniqueName(), 1, 4, null), CancellationToken.None);
        var start = TestClock.MondayMorning.AddMinutes(-30);
        var end = TestClock.MondayMorning.AddMinutes(30);
        db.Bookings.Add(new Booking
        {
            Id = Guid.CreateVersion7(),
            RoomId = room!.Id,
            OrganizerId = "ana",
            Title = "Running now",
            StartAt = start,
            EndAt = end,
            Attendees = 2,
            CreatedAt = start,
        });
        await db.SaveChangesAsync();
        var options = Options.Create(new StreamRoomOccupancyOptions { Interval = TimeSpan.FromMilliseconds(10) });
        var handler = new StreamRoomOccupancyHandler(db, TestClock.Default, options);

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var stream = await handler.HandleAsync(room.Id, cancellation.Token);
        var first = await stream!.FirstAsync(cancellation.Token);

        Assert.Equal(RoomStatus.Occupied, first.Status);
        Assert.Equal(end, first.NextChange);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}