using EntryHub.Api.Data;

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

    public static EntryHubDbContext CreateContext(string? connectionString = null)
    {
        var options = new DbContextOptionsBuilder<EntryHubDbContext>()
            .UseNpgsql(connectionString ?? ConnectionString)
            .Options;

        return new EntryHubDbContext(options);
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
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }
}