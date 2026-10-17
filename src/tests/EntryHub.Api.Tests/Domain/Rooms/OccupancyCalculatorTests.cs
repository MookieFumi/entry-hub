using EntryHub.Api.Domain.Rooms;

namespace EntryHub.Api.Tests.Domain.Rooms;

/// <summary>
/// Layer 1: pure logic. No database, no HTTP, no clock.
/// </summary>
public class OccupancyCalculatorTests
{
    private static readonly Guid RoomId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Room_without_bookings_is_free_with_no_next_change()
    {
        var occupancy = OccupancyCalculator.Calculate(RoomId, [], Now);

        Assert.Equal(RoomStatus.Free, occupancy.Status);
        Assert.Null(occupancy.NextChange);
    }

    [Fact]
    public void Room_is_occupied_until_the_end_of_the_current_booking()
    {
        var busy = new[] { new TimeRange(Now.AddMinutes(-30), Now.AddMinutes(30)) };

        var occupancy = OccupancyCalculator.Calculate(RoomId, busy, Now);

        Assert.Equal(RoomStatus.Occupied, occupancy.Status);
        Assert.Equal(Now.AddMinutes(30), occupancy.NextChange);
    }

    [Fact]
    public void Room_is_free_until_the_start_of_the_next_booking()
    {
        var busy = new[]
        {
            new TimeRange(Now.AddHours(3), Now.AddHours(4)),
            new TimeRange(Now.AddHours(1), Now.AddHours(2)),
        };

        var occupancy = OccupancyCalculator.Calculate(RoomId, busy, Now);

        Assert.Equal(RoomStatus.Free, occupancy.Status);
        Assert.Equal(Now.AddHours(1), occupancy.NextChange);
    }

    [Fact]
    public void Back_to_back_bookings_are_chained()
    {
        var busy = new[]
        {
            new TimeRange(Now.AddMinutes(-15), Now.AddMinutes(15)),
            new TimeRange(Now.AddMinutes(15), Now.AddMinutes(45)),
        };

        var occupancy = OccupancyCalculator.Calculate(RoomId, busy, Now);

        Assert.Equal(RoomStatus.Occupied, occupancy.Status);
        Assert.Equal(Now.AddMinutes(45), occupancy.NextChange);
    }

    [Fact]
    public void A_booking_that_ends_exactly_now_no_longer_counts()
    {
        var busy = new[] { new TimeRange(Now.AddHours(-1), Now) };

        var occupancy = OccupancyCalculator.Calculate(RoomId, busy, Now);

        Assert.Equal(RoomStatus.Free, occupancy.Status);
    }
}