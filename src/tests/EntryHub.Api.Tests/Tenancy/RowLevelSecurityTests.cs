using EntryHub.Api.Data;
using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Domain.Rooms;
using EntryHub.Api.Tenancy;
using EntryHub.Api.Tests.Support;
using EntryHub.Reminders;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace EntryHub.Api.Tests.Tenancy;

/// <summary>
/// The second barrier: PostgreSQL itself hides the rows of other tenants. These tests connect as an ordinary role,
/// because a superuser skips Row-Level Security whatever the policies say.
/// </summary>
[Trait("Category", "Integration")]
public class RowLevelSecurityTests
{
    private const string Role = "entryhub_rls_tester";
    private const string Password = "rls-tester";

    private static readonly Lazy<Task<string>> RestrictedConnection = new(CreateRestrictedRoleAsync);

    private static async Task<string> CreateRestrictedRoleAsync()
    {
        await TestDatabase.EnsureMigratedAsync();

        await using var admin = new NpgsqlConnection(TestDatabase.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand(
            $"""
            DO $$ BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '{Role}') THEN
                    CREATE ROLE {Role} LOGIN PASSWORD '{Password}' NOSUPERUSER NOBYPASSRLS;
                END IF;
            END $$;
            GRANT USAGE ON SCHEMA public TO {Role};
            GRANT SELECT, INSERT, UPDATE, DELETE ON rooms, bookings, tenants TO {Role};
            """,
            admin))
        {
            await create.ExecuteNonQueryAsync();
        }

        return new NpgsqlConnectionStringBuilder(TestDatabase.ConnectionString) { Username = Role, Password = Password }.ConnectionString;
    }

    private static async Task<EntryHubDbContext> RestrictedContextAsync(Guid? tenantId)
    {
        var options = new DbContextOptionsBuilder<EntryHubDbContext>().UseNpgsql(await RestrictedConnection.Value).Options;
        return new EntryHubDbContext(options, new FixedTenantContext(tenantId));
    }

    private static async Task<Room> NewRoomAsync(Guid tenantId)
    {
        await using var db = TestDatabase.CreateContext(tenantId: tenantId);
        var room = new Room { Id = Guid.CreateVersion7(), Name = $"Room {Guid.NewGuid():N}", Floor = 1, Capacity = 4, CreatedAt = TestClock.MondayMorning };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();
        return room;
    }

    [Fact]
    public async Task A_query_that_forgets_the_tenant_filter_still_sees_only_its_own_rows()
    {
        var mine = await NewRoomAsync(TestTenants.Default);
        var theirs = await NewRoomAsync(TestTenants.Other);

        await using var db = await RestrictedContextAsync(TestTenants.Default);
        var visible = await db.Rooms.IgnoreQueryFilters().Select(room => room.Id).ToListAsync();

        Assert.Contains(mine.Id, visible);
        Assert.DoesNotContain(theirs.Id, visible);
    }

    [Fact]
    public async Task A_connection_without_a_tenant_sees_nothing_even_without_the_filter()
    {
        await NewRoomAsync(TestTenants.Default);

        await using var db = await RestrictedContextAsync(tenantId: null);

        Assert.Empty(await db.Rooms.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await db.Bookings.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task The_database_refuses_a_row_written_for_another_tenant()
    {
        await using var db = await RestrictedContextAsync(TestTenants.Default);
        var id = Guid.CreateVersion7();
        var other = TestTenants.Other;
        var name = $"Room {Guid.NewGuid():N}";

        var exception = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"""INSERT INTO rooms ("Id", "TenantId", "Name", "Floor", "Capacity", "Equipment", "IsActive", "CreatedAt") VALUES ({id}, {other}, {name}, 1, 4, ARRAY[]::text[], true, now())"""));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
    }

    [Fact]
    public async Task The_reminders_service_reaches_the_bookings_of_every_tenant()
    {
        var start = new DateTimeOffset(2036, 3, 10, 9, 10, 0, TimeSpan.Zero);
        var bookings = new List<Booking>();
        foreach (var tenant in new[] { TestTenants.Default, TestTenants.Other })
        {
            var room = await NewRoomAsync(tenant);
            await using var db = TestDatabase.CreateContext(tenantId: tenant);
            var booking = new Booking
            {
                Id = Guid.CreateVersion7(),
                RoomId = room.Id,
                OrganizerId = $"person-{Guid.NewGuid():N}",
                Title = "Planning",
                StartAt = start,
                EndAt = start.AddHours(1),
                Attendees = 2,
                CreatedAt = TestClock.MondayMorning,
            };
            db.Bookings.Add(booking);
            await db.SaveChangesAsync();
            bookings.Add(booking);
        }

        // The service connects as the restricted role, so it only sees a tenant after it asks for it.
        var store = new ReminderStore(NpgsqlDataSource.Create(await RestrictedConnection.Value));
        var due = await store.ClaimDueAsync(start.AddMinutes(-10), TimeSpan.FromMinutes(15), CancellationToken.None);

        Assert.Equivalent(bookings.Select(booking => booking.Id), due.Select(reminder => reminder.BookingId));
    }
}