using EntryHub.Api.Data;
using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Domain.Features;
using EntryHub.Api.Domain.Rooms;
using EntryHub.Api.Domain.Sites;
using EntryHub.Api.Domain.Tenants;
using EntryHub.Api.Tenancy;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Demo;

/// <summary>
/// Creates the data of a demo: two customers, each with its own sites, rooms and a few bookings. It can run any number of
/// times: it adds only what is missing, so it never duplicates or changes what a person has edited since.
/// </summary>
public sealed class DemoSeeder(DbContextOptions<EntryHubDbContext> options, TimeProvider clock)
{
    public static readonly Guid AcmeId = new("de000000-0000-0000-0000-0000000000a1");

    public static readonly Guid GlobexId = new("de000000-0000-0000-0000-0000000000a2");

    private const string OfficeTimeZone = "Europe/Madrid";

    private static readonly DemoTenant[] Tenants =
    [
        new(AcmeId, "Acme Corporation", Rules: null,
        [
            new("Madrid HQ", OfficeTimeZone, new(8, 0), new(20, 0),
            [
                new("Aula Magna", 0, 40, ["Projector", "Video conferencing", "Whiteboard"]),
                new("Sala Cervantes", 1, 12, ["Screen", "Video conferencing"]),
                new("Sala Goya", 1, 8, ["Screen", "Whiteboard"]),
                new("Sala Picasso", 2, 6, ["Screen"]),
                new("Cabina 1", 2, 2, []),
                new("Sala Dalí", 3, 20, ["Projector", "Video conferencing", "Wheelchair access"]),
            ]),
            new("London office", "Europe/London", new(9, 0), new(17, 30),
            [
                new("Thames", 1, 10, ["Screen", "Video conferencing"]),
                new("Tower", 2, 6, ["Whiteboard"]),
            ]),
        ]),
        // Globex chose stricter rules than the defaults, so the two demo tenants behave differently.
        new(GlobexId, "Globex Industries", new DemoRules(MaxDurationMinutes: 120, MaxActiveBookings: 3),
        [
            new("Main office", OfficeTimeZone, new(8, 0), new(20, 0),
            [
                new("Boardroom", 5, 16, ["Projector", "Video conferencing"]),
                new("Brainstorm", 4, 10, ["Whiteboard", "Screen"]),
                new("Focus A", 4, 4, ["Screen"]),
                new("Focus B", 4, 4, []),
                new("Training room", 1, 30, ["Projector", "Wheelchair access", "Induction loop"]),
            ]),
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

        var catalog = await db.Features.ToListAsync(cancellationToken);
        foreach (var (name, kind) in DefaultCatalog.Entries.Where(entry => !catalog.Exists(feature => feature.Name == entry.Name)))
        {
            var feature = new Feature { Id = Guid.CreateVersion7(), Code = Feature.CodeFor(name), Name = name, Kind = kind, CreatedAt = now };
            db.Features.Add(feature);
            catalog.Add(feature);
        }

        var existingSites = await db.Sites.ToListAsync(cancellationToken);
        var existingNames = await db.Rooms.Select(room => room.Name).ToListAsync(cancellationToken);

        foreach (var demoSite in tenant.Sites)
        {
            var site = existingSites.Find(existing => existing.Name == demoSite.Name);

            if (site is null)
            {
                site = new Site
                {
                    Id = Guid.CreateVersion7(),
                    Name = demoSite.Name,
                    TimeZoneId = demoSite.TimeZone,
                    Opens = demoSite.Opens,
                    Closes = demoSite.Closes,
                    CreatedAt = now,
                };
                db.Sites.Add(site);
                existingSites.Add(site);
            }

            foreach (var room in demoSite.Rooms.Where(room => !existingNames.Contains(room.Name)))
            {
                db.Rooms.Add(new Room
                {
                    Id = Guid.CreateVersion7(),
                    SiteId = site.Id,
                    Name = room.Name,
                    Floor = room.Floor,
                    Capacity = room.Capacity,
                    Features = [.. room.Equipment.Select(name => catalog.Find(feature => feature.Name == name)!)],
                    CreatedAt = now,
                });
            }
        }

        if (tenant.Rules is { } rules && !await db.TenantRules.AnyAsync(cancellationToken))
        {
            var defaults = new BookingRulesOptions();
            db.TenantRules.Add(new TenantRules
            {
                MinDurationMinutes = (int)defaults.MinDuration.TotalMinutes,
                MaxDurationMinutes = rules.MaxDurationMinutes,
                SlotMinutes = (int)defaults.Slot.TotalMinutes,
                MinLeadTimeMinutes = (int)defaults.MinLeadTime.TotalMinutes,
                MaxAdvanceDays = (int)defaults.MaxAdvance.TotalDays,
                CancellationNoticeMinutes = (int)defaults.CancellationNotice.TotalMinutes,
                MaxActiveBookingsPerEmployee = rules.MaxActiveBookings,
                UpdatedAt = now,
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        // Bookings are dated from today, so they are only created for a tenant that has none: running the seeder again
        // a week later must not pile up a second set.
        if (await db.Bookings.AnyAsync(cancellationToken))
        {
            return;
        }

        var firstSite = existingSites.OrderBy(site => site.CreatedAt).ThenBy(site => site.Id).First();
        var rooms = await db.Rooms.Where(room => room.SiteId == firstSite.Id).OrderBy(room => room.Name).Take(3).ToListAsync(cancellationToken);
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

    private sealed record DemoTenant(Guid Id, string Name, DemoRules? Rules, DemoSite[] Sites);

    private sealed record DemoRules(int MaxDurationMinutes, int MaxActiveBookings);

    private sealed record DemoSite(string Name, string TimeZone, TimeOnly Opens, TimeOnly Closes, DemoRoom[] Rooms);

    private sealed record DemoRoom(string Name, int Floor, int Capacity, string[] Equipment);
}