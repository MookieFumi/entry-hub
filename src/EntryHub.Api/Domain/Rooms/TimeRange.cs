namespace EntryHub.Api.Domain.Rooms;

/// <summary>
/// A half-open interval of time: it includes <see cref="Start"/> and excludes <see cref="End"/>.
/// </summary>
public readonly record struct TimeRange(DateTimeOffset Start, DateTimeOffset End)
{
    public bool Contains(DateTimeOffset moment) => moment >= Start && moment < End;
}