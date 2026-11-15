using EntryHub.Api.Data;
using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Domain.Rooms;
using EntryHub.Api.Domain.Tenants;
using EntryHub.Api.Tenancy;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Demo;

/// <summary>
/// Creates the data of a demo: two customers, each with its own rooms and a few bookings. It can run any number of
/// times: it adds only what is missing, so it never duplicates or changes what a person has edited since.
/// </summary>
public sealed class DemoSeeder(DbContextOptions<EntryHubDbContext> options, TimeProvider clock)
{
    public static readonly Guid AcmeId = new("de000000-0000-0000-0000-0000000000a1");

    public static readonly Guid GlobexId = new("de000000-0000-0000-0000-0000000000a2");

    private const string OfficeTimeZone = "Europe/Madrid";

    private static readonly DemoTenant[] Tenants =
    [
        new(AcmeId, "Acme Corporation",
        [
            new("Aula Magna", 0, 40, ["Projector", "Video conferencing", "Whiteboard"]),
            new("Sala Cervantes", 1, 12, ["Screen", "Video conferencing"]),
            new("Sala Goya", 1, 8, ["Screen", "Whiteboard"]),
            new("Sala Picasso", 2, 6, ["Screen"]),
            new("Cabina 1", 2, 2, []),
            new("Sala Dalí", 3, 20, ["Projector", "Video conferencing", "Wheelchair access"]),
        ]),
        new(GlobexId, "Globex Industries",
        [
            new("Boardroom", 5, 16, ["Projector", "Video conferencing"]),
            new("Brainstorm", 4, 10, ["Whiteboard", "Screen"]),
            new("Focus A", 4, 4, ["Screen"]),
            new("Focus B", 4, 4, []),
            new("Training room", 1, 30, ["Projector", "Wheelchair access", "Induction loop"]),
        ]),
    ];

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        foreach (var tenant in Tenants)
        {
            await SeedTenantAsync(tenant, cancellationToken);
        }
    }

    private async Task SeedTenantAsync(DemoTenant tenant, CancellationToken cancellationToken)
    {
        await using var db = new EntryHubDbContext(options, new FixedTenantContext(tenant.Id));
        var now = clock.GetUtcNow();

        if (!await db.Tenants.AnyAsync(existing => existing.Id == tenant.Id, cancellationToken))
        {
            db.Tenants.Add(new Tenant { Id = tenant.Id, Name = tenant.Name, CreatedAt = now });
            await db.SaveChangesAsync(cancellationToken);
        }

        var existingNames = await db.Rooms.Select(room => room.Name).ToListAsync(cancellationToken);
        foreach (var room in tenant.Rooms.Where(room => !existingNames.Contains(room.Name)))
        {
            db.Rooms.Add(new Room
            {
                Id = Guid.CreateVersion7(),
                Name = room.Name,
                Floor = room.Floor,
                Capacity = room.Capacity,
                Equipment = [.. room.Equipment],
                CreatedAt = now,
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        // Bookings are dated from today, so they are only created for a tenant that has none: running the seeder again
        // a week later must not pile up a second set.
        if (await db.Bookings.AnyAsync(cancellationToken))
        {
            return;
        }

        var rooms = await db.Rooms.OrderBy(room => room.Name).Take(3).ToListAsync(cancellationToken);
        var days = NextWeekdays(now, rooms.Count);
        for (var index = 0; index < rooms.Count; index++)
        {
            var start = days[index];
            db.Bookings.Add(new Booking
            {
                Id = Guid.CreateVersion7(),
                RoomId = rooms[index].Id,
                OrganizerId = index == 0 ? "demo-ana" : "demo-luis",
                Title = BookingTitles[index % BookingTitles.Length],
                StartAt = start,
                EndAt = start.AddHours(1),
                Attendees = Math.Min(rooms[index].Capacity, 4),
                CreatedAt = now,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static readonly string[] BookingTitles = ["Sprint planning", "Customer call", "Design review"];

    /// <summary>
    /// 10:00 office time on the next working days after <paramref name="now"/>.
    /// </summary>
    private static DateTimeOffset[] NextWeekdays(DateTimeOffset now, int count)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(OfficeTimeZone);
        var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).Date);
        var result = new List<DateTimeOffset>();

        while (result.Count < count)
        {
            day = day.AddDays(1);
            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                continue;
            }

            var local = day.ToDateTime(new TimeOnly(10, 0));
            // PostgreSQL stores timestamptz in UTC and Npgsql only accepts an offset of zero.
            result.Add(new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime());
        }

        return [.. result];
    }

    private sealed record DemoTenant(Guid Id, string Name, DemoRoom[] Rooms);

    private sealed record DemoRoom(string Name, int Floor, int Capacity, string[] Equipment);
}