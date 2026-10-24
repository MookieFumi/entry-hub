using System.Threading.Channels;

using GracefulShutdown;

var builder = WebApplication.CreateBuilder(args);

var options = builder.Configuration.GetSection("Jobs").Get<JobOptions>() ?? new JobOptions();
builder.Services.AddSingleton(options);

// How long the host waits for the background services to stop after SIGTERM. The default is 30 seconds.
builder.Services.Configure<HostOptions>(host => host.ShutdownTimeout = TimeSpan.FromSeconds(options.ShutdownTimeoutSeconds));

builder.Services.AddSingleton<Ledger>();
builder.Services.AddKeyedSingleton(JobKind.Confirmation, (_, _) => Channel.CreateUnbounded<Job>());
builder.Services.AddKeyedSingleton(JobKind.Audit, (_, _) => Channel.CreateUnbounded<Job>());
builder.Services.AddHostedService<ConfirmationWorker>();
builder.Services.AddHostedService<AuditWorker>();

var app = builder.Build();

var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() => Log.Stopping(app.Logger));

// Each request leaves two jobs in memory, one for each background service, and answers before they run.
app.MapPost("/jobs", (
    [FromKeyedServices(JobKind.Confirmation)] Channel<Job> confirmations,
    [FromKeyedServices(JobKind.Audit)] Channel<Job> audits) =>
{
    var id = Guid.NewGuid();
    if (lifetime.ApplicationStopping.IsCancellationRequested)
    {
        Log.AcceptedWhileStopping(app.Logger, id);
    }

    confirmations.Writer.TryWrite(new Job(id));
    audits.Writer.TryWrite(new Job(id));
    return Results.Text(id.ToString(), statusCode: StatusCodes.Status202Accepted);
});

app.MapGet("/health", () => "Healthy");

app.Run();