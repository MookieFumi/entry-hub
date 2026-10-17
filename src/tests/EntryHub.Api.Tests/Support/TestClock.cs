using System.Diagnostics;

namespace EntryHub.Api.Tests.Support;

/// <summary>
/// A clock that starts at a chosen moment and then moves with real time. Timers keep using the system timer, so
/// <see cref="PeriodicTimer"/> works, while rules that depend on "now" always see the same Monday morning.
/// </summary>
internal sealed class TestClock(DateTimeOffset start) : TimeProvider
{
    private readonly long _origin = Stopwatch.GetTimestamp();

    /// <summary>
    /// Monday 2026-10-19 07:00 UTC, which is 09:00 in the office (Europe/Madrid, summer time).
    /// </summary>
    public static DateTimeOffset MondayMorning { get; } = new(2026, 10, 19, 7, 0, 0, TimeSpan.Zero);

    public static TestClock Default { get; } = new(MondayMorning);

    public override DateTimeOffset GetUtcNow() => start + Stopwatch.GetElapsedTime(_origin);
}