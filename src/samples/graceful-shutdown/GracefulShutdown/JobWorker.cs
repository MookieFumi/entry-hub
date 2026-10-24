using System.Threading.Channels;

namespace GracefulShutdown;

internal enum JobKind
{
    Confirmation,
    Audit,
}

internal sealed record Job(Guid Id);

internal sealed class JobOptions
{
    /// <summary>What each job takes, standing in for sending an email or calling another service.</summary>
    public int DurationMilliseconds { get; set; } = 300;

    /// <summary>Whether the worker finishes the queued jobs when the host stops, or leaves as soon as it is told to.</summary>
    public bool Drain { get; set; }

    public int ShutdownTimeoutSeconds { get; set; } = 30;
}

// One class per background service: AddHostedService registers each implementation type only once, so two
// registrations of the same class would start a single worker.
internal sealed class ConfirmationWorker(IServiceProvider services) : JobWorker(JobKind.Confirmation, services);

internal sealed class AuditWorker(IServiceProvider services) : JobWorker(JobKind.Audit, services);

internal abstract partial class JobWorker(JobKind kind, IServiceProvider services) : BackgroundService
{
    private readonly ChannelReader<Job> _queue = services.GetRequiredKeyedService<Channel<Job>>(kind).Reader;
    private readonly JobOptions _options = services.GetRequiredService<JobOptions>();
    private readonly Ledger _ledger = services.GetRequiredService<Ledger>();
    private readonly ILogger _logger = services.GetRequiredService<ILoggerFactory>().CreateLogger($"JobWorker.{kind}");

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        _options.Drain ? RunAndDrainAsync(stoppingToken) : RunAsync(stoppingToken);

    // The usual loop: the stopping token goes everywhere, so the job in progress is cancelled and whatever is still
    // queued stays in memory when the process exits.
    private async Task RunAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var job in _queue.ReadAllAsync(stoppingToken))
            {
                await ProcessAsync(job, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            LogStoppedWithQueued(_queue.Count);
        }
    }

    // The same loop with one change: the job gets CancellationToken.None, so the one in progress runs to the end.
    // ReadAllAsync keeps handing out the jobs already queued after the token is cancelled and stops only when the queue
    // is empty, so the stopping token just ends the wait for new jobs.
    private async Task RunAndDrainAsync(CancellationToken stoppingToken)
    {
        var drained = 0;
        try
        {
            await foreach (var job in _queue.ReadAllAsync(stoppingToken))
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    drained++;
                }

                await ProcessAsync(job, CancellationToken.None);
            }
        }
        catch (OperationCanceledException)
        {
            LogDrained(drained);
        }
    }

    private async Task ProcessAsync(Job job, CancellationToken cancellationToken)
    {
        await Task.Delay(_options.DurationMilliseconds, cancellationToken);
        await _ledger.RecordAsync($"{job.Id} {kind}");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Stopped with {Count} jobs still queued")]
    private partial void LogStoppedWithQueued(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stopped after draining {Count} queued jobs")]
    private partial void LogDrained(int count);
}