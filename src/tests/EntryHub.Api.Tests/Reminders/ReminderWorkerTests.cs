using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Domain.Rooms;
using EntryHub.Api.Tests.Support;
using EntryHub.Reminders;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace EntryHub.Api.Tests.Reminders;

[Trait("Category", "Integration")]
public class ReminderWorkerTests
{
    [Fact]
    public async Task A_round_logs_one_reminder_for_a_booking_about_to_start_and_none_afterwards()
    {
        await TestDatabase.EnsureMigratedAsync();
        var now = new DateTimeOffset(2035, 3, 10, 9, 0, 0, TimeSpan.Zero);
        var roomName = $"Room {Guid.NewGuid():N}";
        await using (var db = TestDatabase.CreateContext())
        {
            var room = new Room { Id = Guid.CreateVersion7(), Name = roomName, Floor = 1, Capacity = 8, CreatedAt = TestClock.MondayMorning };
            db.Rooms.Add(room);
            db.Bookings.Add(new Booking
            {
                Id = Guid.CreateVersion7(),
                RoomId = room.Id,
                OrganizerId = "ana",
                Title = "Planning",
                StartAt = now.AddMinutes(10),
                EndAt = now.AddMinutes(70),
                Attendees = 4,
                CreatedAt = TestClock.MondayMorning,
            });
            await db.SaveChangesAsync();
        }

        var logger = new ListLogger();
        var worker = new ReminderWorker(
            new ReminderStore(NpgsqlDataSource.Create(TestDatabase.ConnectionString)),
            Options.Create(new ReminderOptions()),
            new FixedClock(now),
            logger);

        await worker.RemindAsync(TimeSpan.FromMinutes(15), CancellationToken.None);
        await worker.RemindAsync(TimeSpan.FromMinutes(15), CancellationToken.None);

        var message = Assert.Single(logger.Messages);
        Assert.Contains("ana", message, StringComparison.Ordinal);
        Assert.Contains(roomName, message, StringComparison.Ordinal);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class ListLogger : ILogger<ReminderWorker>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}