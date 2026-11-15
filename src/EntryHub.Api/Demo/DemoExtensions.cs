using EntryHub.Api.Data;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Demo;

public static class DemoExtensions
{
    /// <summary>
    /// Registers the demo seeder. It does nothing unless <c>Demo:Seed</c> is <see langword="true"/>.
    /// </summary>
    public static IServiceCollection AddEntryHubDemo(this IServiceCollection services)
    {
        services.AddOptions<DemoSettings>().BindConfiguration("Demo");
        services.AddSingleton(provider =>
            new DemoSeeder(provider.GetRequiredService<DbContextOptions<EntryHubDbContext>>(), provider.GetRequiredService<TimeProvider>()));
        services.AddHostedService<DemoSeedingService>();

        return services;
    }
}