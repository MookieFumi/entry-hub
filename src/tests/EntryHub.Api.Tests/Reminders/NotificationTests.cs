using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;

using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Features.V1.Bookings;
using EntryHub.Api.Features.V1.Bookings.CreateBooking;
using EntryHub.Api.Features.V1.Rooms;
using EntryHub.Api.Features.V1.Rooms.CreateRoom;
using EntryHub.Api.Security;
using EntryHub.Api.Tests.Support;
using EntryHub.Reminders;
using EntryHub.Reminders.Notifications;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace EntryHub.Api.Tests.Reminders;

/// <summary>
/// How reminders reach the organizer: the address that comes from the token, the sender abstraction and what happens
/// when a send fails.
/// </summary>
[Trait("Category", "Integration")]
public class NotificationTests
{
    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private sealed class FakeSender(Func<ReminderMessage, NotificationOutcome> respond) : INotificationSender
    {
        public List<ReminderMessage> Messages { get; } = [];

        public Task<NotificationOutcome> SendReminderAsync(ReminderMessage message, CancellationToken cancellationToken)
        {
            lock (Messages)
            {
                Messages.Add(message);
            }

            return Task.FromResult(respond(message));
        }
    }

    private sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
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

    private static async Task<(Guid Tenant, Guid Booking, string Title)> NewBookingAsync(string? email, DateTimeOffset start)
    {
        var (tenant, site) = await TestDatabase.CreateTenantWithSiteAsync(Unique("Notify"));
        await using var db = TestDatabase.CreateContext(tenantId: tenant);
        var room = new EntryHub.Api.Domain.Rooms.Room { Id = Guid.CreateVersion7(), SiteId = site, Name = "Room", Floor = 1, Capacity = 8, CreatedAt = start.AddDays(-1) };
        db.Rooms.Add(room);
        var title = Unique("Secret plans");
        var booking = new Booking
        {
            Id = Guid.CreateVersion7(),
            RoomId = room.Id,
            OrganizerId = Unique("person"),
            OrganizerEmail = email,
            Title = title,
            StartAt = start,
            EndAt = start.AddHours(1),
            Attendees = 4,
            CreatedAt = start.AddDays(-1),
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();
        return (tenant, booking.Id, title);
    }

    private static ReminderWorker NewWorker(INotificationSender sender, ManualClock clock, ListLogger? logger = null) =>
        new(
            new ReminderStore(NpgsqlDataSource.Create(TestDatabase.ConnectionString)),
            sender,
            Options.Create(new ReminderOptions { RetryAfter = TimeSpan.FromMinutes(1) }),
            clock,
            logger ?? new ListLogger());

    private static async Task<DateTimeOffset?> ReminderSentAtAsync(Guid tenant, Guid booking)
    {
        await using var db = TestDatabase.CreateContext(tenantId: tenant);
        return await db.Bookings.Where(item => item.Id == booking).Select(item => item.ReminderSentAt).SingleAsync();
    }

    [Fact]
    public async Task The_email_claim_of_the_token_is_kept_in_the_booking_the_organizer_makes()
    {
        var tenant = await TestDatabase.CreateTenantAsync(Unique("tenant"));
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientForTenant(tenant, Unique("fran"), Roles.Facilities);
        var room = (await (await facilities.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(Unique("Room"), 1, 8, null))).Content.ReadFromJsonAsync<RoomResponse>(TestJson.Options))!;
        var subject = Unique("ana");
        using var ana = api.ClientWithToken(TestTokens.CreateWithEmail(tenant, subject, "ana@example.com", Roles.Employee));
        var start = new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);

        var own = await (await ana.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(room.Id, "Own", start, start.AddHours(1), 2))).Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options);
        var onBehalf = await (await facilities.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(room.Id, "For someone", start.AddHours(2), start.AddHours(3), 2, OnBehalfOf: Unique("bea")))).Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options);

        await using var db = TestDatabase.CreateContext(tenantId: tenant);
        Assert.Equal("ana@example.com", await db.Bookings.Where(booking => booking.Id == own!.Id).Select(booking => booking.OrganizerEmail).SingleAsync());
        Assert.Null(await db.Bookings.Where(booking => booking.Id == onBehalf!.Id).Select(booking => booking.OrganizerEmail).SingleAsync());
        Assert.DoesNotContain("ana@example.com", await ana.GetStringAsync("/api/v1/bookings/mine"));
    }

    [Fact]
    public async Task A_reminder_goes_to_the_sender_with_the_address_and_is_marked_when_it_is_sent()
    {
        var start = new DateTimeOffset(2036, 5, 5, 9, 0, 0, TimeSpan.Zero);
        var (tenant, booking, title) = await NewBookingAsync("ana@example.com", start);
        var sender = new FakeSender(_ => NotificationOutcome.Sent);
        var clock = new ManualClock(start.AddMinutes(-10));
        var logger = new ListLogger();

        await NewWorker(sender, clock, logger).RemindAsync(TimeSpan.FromMinutes(15), CancellationToken.None);

        var message = Assert.Single(sender.Messages, item => item.BookingId == booking);
        Assert.Equal(("ana@example.com", title), (message.Email, message.Title));
        Assert.NotNull(await ReminderSentAtAsync(tenant, booking));
        Assert.DoesNotContain(logger.Messages, line => line.Contains("ana@example.com", StringComparison.Ordinal) || line.Contains(title, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_failed_send_does_not_mark_the_reminder_and_is_tried_again_after_the_retry_time()
    {
        var start = new DateTimeOffset(2036, 6, 5, 9, 0, 0, TimeSpan.Zero);
        var (tenant, booking, _) = await NewBookingAsync("ana@example.com", start);
        var healthy = false;
        var sender = new FakeSender(_ => healthy ? NotificationOutcome.Sent : NotificationOutcome.Failed);
        var clock = new ManualClock(start.AddMinutes(-10));
        var worker = NewWorker(sender, clock);

        await worker.RemindAsync(TimeSpan.FromMinutes(15), CancellationToken.None);
        Assert.Null(await ReminderSentAtAsync(tenant, booking));

        clock.Now = start.AddMinutes(-10).AddSeconds(30);
        await worker.RemindAsync(TimeSpan.FromMinutes(15), CancellationToken.None);
        Assert.Single(sender.Messages, item => item.BookingId == booking);

        healthy = true;
        clock.Now = start.AddMinutes(-8);
        await worker.RemindAsync(TimeSpan.FromMinutes(15), CancellationToken.None);

        Assert.Equal(2, sender.Messages.Count(item => item.BookingId == booking));
        Assert.NotNull(await ReminderSentAtAsync(tenant, booking));
    }

    [Fact]
    public async Task A_booking_without_an_address_is_counted_and_done_but_not_failed()
    {
        var start = new DateTimeOffset(2036, 7, 5, 9, 0, 0, TimeSpan.Zero);
        var (tenant, booking, _) = await NewBookingAsync(email: null, start);
        var sender = new FakeSender(message => message.Email is null ? NotificationOutcome.NoRecipient : NotificationOutcome.Sent);
        var logger = new ListLogger();

        await NewWorker(sender, new ManualClock(start.AddMinutes(-10)), logger).RemindAsync(TimeSpan.FromMinutes(15), CancellationToken.None);

        Assert.NotNull(await ReminderSentAtAsync(tenant, booking));
        Assert.Contains("Reminders: 0 sent, 1 without an address, 0 to try again", logger.Messages);
    }

    [Fact]
    public async Task The_smtp_sender_has_nowhere_to_send_without_an_address_and_fails_quietly_when_the_server_is_down()
    {
        var port = FreePort();
        var options = Options.Create(new NotificationOptions
        {
            Smtp = new SmtpOptions { Enabled = true, Host = "127.0.0.1", Port = port, UseSsl = false, From = "reminders@example.com", Timeout = TimeSpan.FromSeconds(2) },
        });
        var logger = new CapturingLogger<SmtpNotificationSender>();
        var sender = new SmtpNotificationSender(options, logger);
        var withAddress = new ReminderMessage(Guid.NewGuid(), "ana", "ana@example.com", "Secret plans", "Room", DateTimeOffset.UnixEpoch);

        Assert.Equal(NotificationOutcome.NoRecipient, await sender.SendReminderAsync(withAddress with { Email = null }, CancellationToken.None));
        Assert.Equal(NotificationOutcome.Failed, await sender.SendReminderAsync(withAddress, CancellationToken.None));

        var line = Assert.Single(logger.Messages);
        Assert.DoesNotContain("ana@example.com", line, StringComparison.Ordinal);
        Assert.DoesNotContain("Secret plans", line, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, "smtp.example.com", "reminders@example.com", false)]
    [InlineData(true, null, "reminders@example.com", false)]
    [InlineData(true, "smtp.example.com", null, false)]
    [InlineData(true, "smtp.example.com", "reminders@example.com", true)]
    public void Smtp_is_only_used_when_it_is_on_and_complete(bool enabled, string? host, string? from, bool usable) =>
        Assert.Equal(usable, new SmtpOptions { Enabled = enabled, Host = host, From = from }.IsUsable);

    [Fact]
    public void Smtp_is_off_by_default() => Assert.False(new NotificationOptions().Smtp.IsUsable);

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}