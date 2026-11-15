using EntryHub.Api.Data;
using EntryHub.Api.Tenancy;
using EntryHub.Api.Tests.Support;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using Npgsql;

namespace EntryHub.Api.Tests.Tenancy;

/// <summary>
/// Migrations that rewrite data are run against a scratch database that already has rows, which is what an existing
/// installation looks like. An empty test database would never exercise the back-fill.
/// </summary>
[Trait("Category", "Integration")]
public class MigrationDataTests
{
    private const string BeforeSites = "20261114201203_AddRowLevelSecurity";

    private const string BeforeCatalog = "20261115063930_AddSitesAndTenantRules";

    private static readonly Guid TenantId = new("c0000000-0000-0000-0000-00000000000c");

    [Fact]
    public async Task Existing_rooms_get_a_site_and_their_free_text_equipment_becomes_the_catalog()
    {
        await TestDatabase.EnsureMigratedAsync();
        var scratch = $"entryhub_migration_{Guid.NewGuid():N}";
        var admin = new NpgsqlConnectionStringBuilder(TestDatabase.ConnectionString);
        var scratchConnection = new NpgsqlConnectionStringBuilder(TestDatabase.ConnectionString) { Database = scratch, Pooling = false }.ConnectionString;

        await ExecuteAsync(admin.ConnectionString, $"CREATE DATABASE {scratch}");

        try
        {
            var options = new DbContextOptionsBuilder<EntryHubDbContext>().UseNpgsql(scratchConnection).Options;
            await using var db = new EntryHubDbContext(options, new FixedTenantContext(TenantId));
            var migrator = db.GetService<IMigrator>();

            await migrator.MigrateAsync(BeforeSites);
            await ExecuteAsync(scratchConnection, """
                INSERT INTO tenants ("Id", "Name", "CreatedAt") VALUES ('c0000000-0000-0000-0000-00000000000c', 'Old customer', now());
                ALTER TABLE rooms NO FORCE ROW LEVEL SECURITY;
                INSERT INTO rooms ("Id", "TenantId", "Name", "Floor", "Capacity", "Equipment", "IsActive", "CreatedAt") VALUES
                    ('c1000000-0000-0000-0000-000000000001', 'c0000000-0000-0000-0000-00000000000c', 'Hall', 1, 20, ARRAY['Projector', 'Wheelchair access', 'Video  conferencing'], true, now()),
                    ('c1000000-0000-0000-0000-000000000002', 'c0000000-0000-0000-0000-00000000000c', 'Booth', 1, 2, ARRAY['projector', ' ', 'Screen'], true, now()),
                    ('c1000000-0000-0000-0000-000000000003', 'c0000000-0000-0000-0000-00000000000c', 'Empty', 1, 4, ARRAY[]::text[], true, now());
                ALTER TABLE rooms FORCE ROW LEVEL SECURITY;
                """);

            await migrator.MigrateAsync(BeforeCatalog);
            await using (var afterSites = new EntryHubDbContext(options, new FixedTenantContext(TenantId)))
            {
                var site = await afterSites.Sites.SingleAsync();
                Assert.Equal(("Main office", "Europe/Madrid"), (site.Name, site.TimeZoneId));
                Assert.All(await afterSites.Rooms.IgnoreAutoIncludes().ToListAsync(), room => Assert.Equal(site.Id, room.SiteId));
            }

            await migrator.MigrateAsync();

            await using var after = new EntryHubDbContext(options, new FixedTenantContext(TenantId));
            var features = await after.Features.OrderBy(feature => feature.Code).ToListAsync();
            var rooms = await after.Rooms.OrderBy(room => room.Name).ToListAsync();

            Assert.Equal(
                [("projector", "Equipment"), ("screen", "Equipment"), ("video-conferencing", "Equipment"), ("wheelchair-access", "Accessibility")],
                features.Select(feature => (feature.Code, feature.Kind.ToString())));
            Assert.Equal(["Booth", "Empty", "Hall"], rooms.Select(room => room.Name));
            Assert.Equal(["Projector", "Screen"], rooms[0].Features.Select(feature => feature.Name).Order());
            Assert.Empty(rooms[1].Features);
            Assert.Equal(["Projector", "Video  conferencing", "Wheelchair access"], rooms[2].Features.Select(feature => feature.Name).Order());
        }
        finally
        {
            await ExecuteAsync(admin.ConnectionString, $"DROP DATABASE IF EXISTS {scratch} WITH (FORCE)");
        }
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}