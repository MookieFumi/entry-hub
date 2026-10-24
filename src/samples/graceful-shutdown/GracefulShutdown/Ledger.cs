namespace GracefulShutdown;

/// <summary>
/// One line per processed job, appended to a file on a volume both pods mount, so the jobs a pod processed are still
/// countable after the pod is gone.
/// </summary>
internal sealed class Ledger(IConfiguration configuration) : IDisposable
{
    private readonly string _path = configuration["Ledger:Path"] ?? Path.Combine(Path.GetTempPath(), "graceful-shutdown.log");
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task RecordAsync(string line)
    {
        await _lock.WaitAsync();
        try
        {
            await File.AppendAllTextAsync(_path, line + "\n");
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose() => _lock.Dispose();
}