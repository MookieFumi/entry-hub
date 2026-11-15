using EntryHub.Api.Data;
using EntryHub.Api.Demo;
using EntryHub.Api.Tests.Support;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Tests.Demo;

[Trait("Category", "Integration")]
[Collection("demo data")]
public class DemoSeederTests
{
    private static DemoSeeder Seeder()
    {
        var options = new DbContextOptionsBuilder<EntryHubDbContext>().UseNpgsql(TestDatabase.ConnectionString).Options;
        return new DemoSeeder(options, TestClock.Default);
    }

    [Fact]
    public async Task Seeding_twice_creates_the_data_once_and_each_tenant_only_sees_its_own()
    {
        await TestDatabase.EnsureMigratedAsync();
        var seeder = Seeder();

        await seeder.SeedAsync(CancellationToken.None);
        await seeder.SeedAsync(CancellationToken.None);

        await using var acme = TestDatabase.CreateContext(tenantId: DemoSeeder.AcmeId);
        await using var globex = TestDatabase.CreateContext(tenantId: DemoSeeder.GlobexId);
        var acmeRooms = await acme.Rooms.ToListAsync();
        var globexRooms = await globex.Rooms.ToListAsync();

        Assert.Equal(8, acmeRooms.Count);
        Assert.Equal(5, globexRooms.Count);
        Assert.Empty(acmeRooms.Select(room => room.Name).Intersect(globexRooms.Select(room => room.Name)));
        Assert.Equal(3, await acme.Bookings.CountAsync());
        Assert.Equal(3, await globex.Bookings.CountAsync());
    }

    [Fact]
    public async Task Both_demo_tenants_start_with_the_default_catalog_and_their_rooms_use_it()
    {
        await TestDatabase.EnsureMigratedAsync();
        await Seeder().SeedAsync(CancellationToken.None);

        await using var acme = TestDatabase.CreateContext(tenantId: DemoSeeder.AcmeId);
        await using var globex = TestDatabase.CreateContext(tenantId: DemoSeeder.GlobexId);

        Assert.Equal(6, await acme.Features.CountAsync());
        Assert.Equal(6, await globex.Features.CountAsync());
        var hall = await acme.Rooms.SingleAsync(room => room.Name == "Aula Magna");
        Assert.Equal(["Projector", "Video conferencing", "Whiteboard"], hall.Features.Select(feature => feature.Name).Order());
        Assert.Empty((await acme.Rooms.SingleAsync(room => room.Name == "Cabina 1")).Features);
    }

    [Fact]
    public async Task The_demo_tenants_differ_in_sites_and_in_rules()
    {
        await TestDatabase.EnsureMigratedAsync();
        await Seeder().SeedAsync(CancellationToken.None);

        await using var acme = TestDatabase.CreateContext(tenantId: DemoSeeder.AcmeId);
        await using var globex = TestDatabase.CreateContext(tenantId: DemoSeeder.GlobexId);

        Assert.Equal(["Europe/London", "Europe/Madrid"], (await acme.Sites.Select(site => site.TimeZoneId).ToListAsync()).Order());
        Assert.Empty(await acme.TenantRules.ToListAsync());
        Assert.Equal(120, (await globex.TenantRules.SingleAsync()).MaxDurationMinutes);
    }

    [Fact]
    public async Task The_seeded_bookings_are_on_working_days_inside_office_hours()
    {
        await TestDatabase.EnsureMigratedAsync();
        await Seeder().SeedAsync(CancellationToken.None);
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid");

        await using var acme = TestDatabase.CreateContext(tenantId: DemoSeeder.AcmeId);
        var bookings = await acme.Bookings.ToListAsync();

        Assert.All(bookings, booking =>
        {
            var local = TimeZoneInfo.ConvertTime(booking.StartAt, zone);
            Assert.NotEqual(DayOfWeek.Saturday, local.DayOfWeek);
            Assert.NotEqual(DayOfWeek.Sunday, local.DayOfWeek);
            Assert.Equal(10, local.Hour);
            Assert.True(booking.StartAt > TestClock.Default.GetUtcNow());
        });
    }

    [Fact]
    public async Task The_seeder_does_not_touch_what_a_person_changed()
    {
        await TestDatabase.EnsureMigratedAsync();
        var seeder = Seeder();
        await seeder.SeedAsync(CancellationToken.None);

        await using (var acme = TestDatabase.CreateContext(tenantId: DemoSeeder.AcmeId))
        {
            var room = await acme.Rooms.SingleAsync(room => room.Name == "Cabina 1");
            room.Capacity = 3;
            room.IsActive = false;
            await acme.SaveChangesAsync();
        }

        await seeder.SeedAsync(CancellationToken.None);

        await using var check = TestDatabase.CreateContext(tenantId: DemoSeeder.AcmeId);
        var cabin = await check.Rooms.SingleAsync(room => room.Name == "Cabina 1");
        Assert.Equal((3, false), (cabin.Capacity, cabin.IsActive));
    }
}