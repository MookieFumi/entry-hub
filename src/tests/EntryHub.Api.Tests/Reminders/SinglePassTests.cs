using EntryHub.Reminders;

using Microsoft.Extensions.Logging.Abstractions;

namespace EntryHub.Api.Tests.Reminders;

public class SinglePassTests
{
    [Fact]
    public async Task Both_tasks_run_once_and_the_exit_code_is_zero()
    {
        var calls = new List<string>();

        var code = await SinglePass.RunAsync(
            _ => { calls.Add("reminders"); return Task.CompletedTask; },
            _ => { calls.Add("webhooks"); return Task.CompletedTask; },
            NullLogger.Instance,
            CancellationToken.None);

        Assert.Equal(0, code);
        Assert.Equal(["reminders", "webhooks"], calls);
    }

    [Fact]
    public async Task A_failure_in_the_first_task_does_not_skip_the_second_and_gives_exit_code_one()
    {
        var webhooksRan = false;

        var code = await SinglePass.RunAsync(
            _ => throw new InvalidOperationException("database down"),
            _ => { webhooksRan = true; return Task.CompletedTask; },
            NullLogger.Instance,
            CancellationToken.None);

        Assert.Equal(1, code);
        Assert.True(webhooksRan);
    }

    [Fact]
    public async Task A_failure_in_the_second_task_gives_exit_code_one()
    {
        var code = await SinglePass.RunAsync(
            _ => Task.CompletedTask,
            _ => throw new InvalidOperationException("boom"),
            NullLogger.Instance,
            CancellationToken.None);

        Assert.Equal(1, code);
    }

    [Fact]
    public async Task Cancellation_is_not_swallowed()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SinglePass.RunAsync(
            token => Task.FromCanceled(token),
            _ => Task.CompletedTask,
            NullLogger.Instance,
            cancelled.Token));
    }
}