using EntryHub.Reminders;
using EntryHub.Reminders.Notifications;
using EntryHub.Reminders.Webhooks;

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

        // Reminders are logged unless the SMTP settings are on and complete.
        builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection(NotificationOptions.SectionName));
        if (builder.Configuration.GetSection(NotificationOptions.SectionName).Get<NotificationOptions>()?.Smtp.IsUsable == true)
        {
            builder.Services.AddSingleton<INotificationSender, SmtpNotificationSender>();
        }
        else
        {
            builder.Services.AddSingleton<INotificationSender, LogNotificationSender>();
        }

        // With Reminders:RunOnce the service does one pass and exits (for a scheduled job), so nothing runs in the
        // background: the workers are only resolved and called once.
        var runOnce = builder.Configuration.GetSection(ReminderOptions.SectionName).Get<ReminderOptions>()?.RunOnce == true;

        builder.Services.AddSingleton<ReminderWorker>();

        // The webhook client is built by hand, not by IHttpClientFactory: the defaults of the service (retries,
        // service discovery) must not apply to requests that go to other people's servers.
        builder.Services.Configure<WebhookOptions>(builder.Configuration.GetSection(WebhookOptions.SectionName));
        builder.Services.AddSingleton<WebhookDeliveryStore>();
        builder.Services.AddSingleton(provider => new WebhookSender(
            WebhookHttp.CreateClient(provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<WebhookOptions>>().Value.Timeout),
            provider.GetRequiredService<TimeProvider>()));
        builder.Services.AddSingleton<WebhookWorker>();

        if (!runOnce)
        {
            builder.Services.AddHostedService(provider => provider.GetRequiredService<ReminderWorker>());
            builder.Services.AddHostedService(provider => provider.GetRequiredService<WebhookWorker>());
        }

        using var host = builder.Build();

        if (runOnce)
        {
            var settings = host.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<ReminderOptions>>().Value;
            var reminders = host.Services.GetRequiredService<ReminderWorker>();
            var webhooks = host.Services.GetRequiredService<WebhookWorker>();

            Environment.ExitCode = await SinglePass.RunAsync(
                token => reminders.RemindAsync(settings.LeadTime, token),
                webhooks.DeliverAsync,
                host.Services.GetRequiredService<ILogger<ReminderWorker>>(),
                CancellationToken.None);
            return;
        }

        await host.RunAsync();
    }
}