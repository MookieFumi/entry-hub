namespace EntryHub.Reminders;

/// <summary>
/// One pass of the service for a scheduled job: every task runs once, a failure in one does not skip the others, and
/// the result is an exit code the scheduler can act on.
/// </summary>
public static partial class SinglePass
{
    public static async Task<int> RunAsync(
        Func<CancellationToken, Task> reminders,
        Func<CancellationToken, Task> webhooks,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var failed = false;

        foreach (var task in new[] { reminders, webhooks })
        {
            try
            {
                await task(cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failed = true;
                LogFailed(logger, exception);
            }
        }

        return failed ? 1 : 0;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Part of the single pass failed")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}