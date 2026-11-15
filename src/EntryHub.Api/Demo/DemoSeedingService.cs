using Microsoft.Extensions.Options;

namespace EntryHub.Api.Demo;

/// <summary>
/// Runs <see cref="DemoSeeder"/> once at startup when <c>Demo:Seed</c> is on. A failure is logged and does not stop
/// the API: the demo data is a convenience, not something the API needs to serve requests.
/// </summary>
public sealed partial class DemoSeedingService(
    DemoSeeder seeder,
    IOptions<DemoSettings> settings,
    ILogger<DemoSeedingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.Value.Seed)
        {
            return;
        }

        try
        {
            await seeder.SeedAsync(stoppingToken);
            LogSeeded(logger);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailed(logger, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Demo data is in place: two tenants with rooms and bookings.")]
    private static partial void LogSeeded(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "The demo data could not be created.")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}