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

        builder.Services.AddSingleton<ReminderWorker>();
        builder.Services.AddHostedService(provider => provider.GetRequiredService<ReminderWorker>());

        // The webhook client is built by hand, not by IHttpClientFactory: the defaults of the service (retries,
        // service discovery) must not apply to requests that go to other people's servers.
        builder.Services.Configure<WebhookOptions>(builder.Configuration.GetSection(WebhookOptions.SectionName));
        builder.Services.AddSingleton<WebhookDeliveryStore>();
        builder.Services.AddSingleton(provider => new WebhookSender(
            WebhookHttp.CreateClient(provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<WebhookOptions>>().Value.Timeout),
            provider.GetRequiredService<TimeProvider>()));
        builder.Services.AddHostedService<WebhookWorker>();

        await builder.Build().RunAsync();
    }
}