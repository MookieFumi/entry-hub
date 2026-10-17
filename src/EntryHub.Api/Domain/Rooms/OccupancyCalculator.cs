namespace EntryHub.Api.Domain.Rooms;

/// <summary>
/// Decides whether a room is busy at a given moment. It has no dependencies, so it is tested without a database or a clock.
/// </summary>
public static class OccupancyCalculator
{
    public static RoomOccupancy Calculate(Guid roomId, IReadOnlyCollection<TimeRange> busy, DateTimeOffset now)
    {
        var current = busy.Where(range => range.Contains(now)).OrderByDescending(range => range.End).FirstOrDefault();

        if (current != default)
        {
            // Back-to-back ranges are chained so "free from" is the first moment nobody has the room.
            var freeFrom = current.End;
            foreach (var next in busy.Where(range => range.Start <= freeFrom).OrderBy(range => range.Start))
            {
                if (next.End > freeFrom)
                {
                    freeFrom = next.End;
                }
            }

            return new RoomOccupancy(roomId, RoomStatus.Occupied, now, freeFrom);
        }

        var upcoming = busy.Where(range => range.Start > now).OrderBy(range => range.Start).FirstOrDefault();

        return new RoomOccupancy(roomId, RoomStatus.Free, now, upcoming == default ? null : upcoming.Start);
    }
}

public enum RoomStatus
{
    Free,
    Occupied,
}

/// <param name="RoomId">The room the snapshot describes.</param>
/// <param name="Status">Whether the room is in use at <paramref name="At"/>.</param>
/// <param name="At">The moment the snapshot was taken.</param>
/// <param name="NextChange">When the status will change: the end of the current use, or the start of the next one.</param>
public sealed record RoomOccupancy(Guid RoomId, RoomStatus Status, DateTimeOffset At, DateTimeOffset? NextChange);