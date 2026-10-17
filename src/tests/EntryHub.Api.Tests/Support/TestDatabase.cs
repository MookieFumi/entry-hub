using EntryHub.Api.Data;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Tests.Support;

/// <summary>
/// Connection settings of the PostgreSQL used by integration tests.
/// The default matches the local Docker Compose file; CI and other machines set <c>ENTRYHUB_TEST_CONNECTION</c>.
/// </summary>
internal static class TestDatabase
{
    private const string EnvironmentVariable = "ENTRYHUB_TEST_CONNECTION";

    private const string LocalDefault =
        "Host=localhost;Port=5432;Database=entryhub;Username=entryhub;Password=entryhub";

    public static string ConnectionString =>
        Environment.GetEnvironmentVariable(EnvironmentVariable) ?? LocalDefault;

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

    private static async Task MigrateAsync()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }
}