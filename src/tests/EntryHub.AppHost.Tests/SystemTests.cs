using System.Net;
using System.Net.Http.Json;

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

using EntryHub.Api.Data;
using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Domain.Rooms;
using EntryHub.Api.Features.V1.Rooms;
using EntryHub.Api.Features.V1.Rooms.CreateRoom;
using EntryHub.Api.Security;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EntryHub.AppHost.Tests;

/// <summary>
/// Tests that start everything the AppHost starts, so they need a container runtime. They are tagged so the
/// regular build can leave them out.
/// </summary>
[Collection(SystemCollectionDefinition.Name)]
[Trait("Category", "Aspire")]
public class SystemTests(EntryHubSystem system)
{
    private static readonly TimeSpan ReminderTimeout = TimeSpan.FromMinutes(2);

    [Fact]
    public async Task The_api_is_healthy_and_serves_rooms_created_through_it()
    {
        using var facilities = system.CreateApiClient("facilities", Roles.Facilities);

        var health = await facilities.GetAsync("/health");
        var created = await facilities.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest($"Room {Guid.NewGuid():N}", 2, 6, null));

        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var room = await created.Content.ReadFromJsonAsync<RoomResponse>();
        var read = await facilities.GetAsync($"/api/v1/rooms/{room!.Id}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
    }

    [Fact]
    public async Task The_api_rejects_requests_without_a_token()
    {
        using var anonymous = system.App.CreateHttpClient("api", "http");

        var response = await anonymous.GetAsync("/api/v1/rooms");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task The_reminders_service_logs_a_reminder_for_a_booking_about_to_start()
    {
        var connectionString = await system.GetConnectionStringAsync(CancellationToken.None);
        var options = new DbContextOptionsBuilder<EntryHubDbContext>().UseNpgsql(connectionString).Options;
        var roomName = $"Room {Guid.NewGuid():N}";
        var organizer = $"ana-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;

        // The booking rules only allow office hours, so the booking goes straight into the database: what is
        // under test is the reminders service reading it, not how the API creates it.
        await using (var db = new EntryHubDbContext(options))
        {
            var room = new Room { Id = Guid.CreateVersion7(), Name = roomName, Floor = 1, Capacity = 8, CreatedAt = now };
            db.Rooms.Add(room);
            db.Bookings.Add(new Booking
            {
                Id = Guid.CreateVersion7(),
                RoomId = room.Id,
                OrganizerId = organizer,
                Title = "Planning",
                StartAt = now.AddMinutes(5),
                EndAt = now.AddMinutes(65),
                Attendees = 4,
                CreatedAt = now,
            });
            await db.SaveChangesAsync();
        }

        using var timeout = new CancellationTokenSource(ReminderTimeout);
        var logs = system.App.Services.GetRequiredService<ResourceLoggerService>();
        var found = false;
        await foreach (var batch in logs.WatchAsync("reminders").WithCancellation(timeout.Token))
        {
            if (batch.Any(line => line.Content.Contains(organizer, StringComparison.Ordinal) && line.Content.Contains(roomName, StringComparison.Ordinal)))
            {
                found = true;
                break;
            }
        }

        Assert.True(found, "The reminders service did not log the reminder.");
    }
}