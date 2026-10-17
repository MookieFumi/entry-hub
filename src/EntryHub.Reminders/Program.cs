using EntryHub.Reminders;

// An explicit entry point keeps this assembly from declaring a type called Program, which would clash with the
// one the API exposes to its tests.
internal static class ReminderService
{
    private static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.AddServiceDefaults();
        builder.AddNpgsqlDataSource("EntryHub");

        builder.Services.Configure<ReminderOptions>(builder.Configuration.GetSection(ReminderOptions.SectionName));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<ReminderStore>();
        builder.Services.AddSingleton<ReminderWorker>();
        builder.Services.AddHostedService(provider => provider.GetRequiredService<ReminderWorker>());

        await builder.Build().RunAsync();
    }
}