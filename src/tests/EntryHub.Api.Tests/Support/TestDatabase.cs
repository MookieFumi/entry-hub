using EntryHub.Api.Data;
using EntryHub.Api.Tenancy;

using Microsoft.EntityFrameworkCore;

using Testcontainers.PostgreSql;

namespace EntryHub.Api.Tests.Support;

/// <summary>
/// Connection settings of the PostgreSQL used by integration tests.
/// By default the first test that needs it starts a PostgreSQL container with Testcontainers, shared by the whole run;
/// Testcontainers removes it when the run ends. Set <c>ENTRYHUB_TEST_CONNECTION</c> to use a PostgreSQL of your own
/// instead, on a machine without Docker for example.
/// </summary>
internal static class TestDatabase
{
    private const string EnvironmentVariable = "ENTRYHUB_TEST_CONNECTION";

    // The image the CI service container used before these tests started their own.
    private const string Image = "postgres:17-alpine";

    private static readonly Lazy<string> Connection = new(() =>
        Environment.GetEnvironmentVariable(EnvironmentVariable) ?? StartContainer());

    public static string ConnectionString => Connection.Value;

    /// <summary>
    /// A connection string that points to a port nobody listens on, with a short timeout.
    /// </summary>
    public const string Unreachable =
        "Host=localhost;Port=1;Database=entryhub;Username=entryhub;Password=entryhub;Timeout=1;Command Timeout=1";

    private static readonly Lazy<Task> Migrated = new(MigrateAsync);

    /// <summary>
    /// Applies the migrations once per test run, so tests that touch the schema can run in any order.
    /// </summary>
    public static Task EnsureMigratedAsync() => Migrated.Value;

    /// <summary>
    /// A context for one tenant, <see cref="TestTenants.Default"/> unless the test says otherwise.
    /// </summary>
    public static EntryHubDbContext CreateContext(string? connectionString = null, Guid? tenantId = null)
    {
        if (connectionString is null)
        {
            // The first test to touch the database may be one that never started the API, so the schema and the shared
            // tenants must exist before any context writes. The tests of one run share them.
            Migrated.Value.GetAwaiter().GetResult();
        }

        return Create(connectionString ?? ConnectionString, tenantId ?? TestTenants.Default);
    }

    /// <summary>
    /// A tenant of its own with one site, for tests that change or delete everything the tenant has and so cannot
    /// share <see cref="TestTenants.Default"/> with the rest of the run.
    /// </summary>
    public static async Task<Guid> CreateTenantAsync(string name) => (await CreateTenantWithSiteAsync(name)).Tenant;

    /// <inheritdoc cref="CreateTenantAsync"/>
    public static async Task<(Guid Tenant, Guid Site)> CreateTenantWithSiteAsync(string name)
    {
        await EnsureMigratedAsync();
        var id = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        await using var db = Create(ConnectionString, id);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO tenants (\"Id\", \"Name\", \"CreatedAt\") VALUES ({id}, {name}, now())");
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO sites ("Id", "TenantId", "Name", "TimeZoneId", "Opens", "Closes", "OpenDays", "IsActive", "CreatedAt")
            VALUES ({siteId}, {id}, 'Main office', 'Europe/Madrid', '08:00', '20:00', ARRAY[1, 2, 3, 4, 5], true, '2000-01-01 00:00:00+00')
            """);
        return (id, siteId);
    }

    private static EntryHubDbContext Create(string connectionString, Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<EntryHubDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new EntryHubDbContext(options, new FixedTenantContext(tenantId));
    }

    private static string StartContainer()
    {
        var container = new PostgreSqlBuilder(Image)
            .WithDatabase("entryhub")
            .WithUsername("entryhub")
            .WithPassword("entryhub")
            .Build();

        // Lazy<T> needs a synchronous factory. It runs once per test run, before any test touches the database.
        container.StartAsync().GetAwaiter().GetResult();
        return container.GetConnectionString();
    }

    private static async Task MigrateAsync()
    {
        await using var db = Create(ConnectionString, TestTenants.Default);
        await db.Database.MigrateAsync();

        // Rooms and bookings need their tenant to exist. The tests share these two across the whole run.
        foreach (var (id, name) in new[] { (TestTenants.Default, "Tests A"), (TestTenants.Other, "Tests B") })
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO tenants (\"Id\", \"Name\", \"CreatedAt\") VALUES ({id}, {name}, now()) ON CONFLICT DO NOTHING");

            // The oldest active site of a tenant is where new rooms go, so these must be the oldest.
            await using var forTenant = Create(ConnectionString, id);
            await forTenant.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO sites ("Id", "TenantId", "Name", "TimeZoneId", "Opens", "Closes", "OpenDays", "IsActive", "CreatedAt")
                VALUES ({TestSites.For(id)}, {id}, 'Tests main office', 'Europe/Madrid', '08:00', '20:00', ARRAY[1, 2, 3, 4, 5], true, '2000-01-01 00:00:00+00')
                ON CONFLICT DO NOTHING
                """);
        }
    }
}