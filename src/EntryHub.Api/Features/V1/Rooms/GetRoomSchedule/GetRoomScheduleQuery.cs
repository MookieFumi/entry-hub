namespace EntryHub.Api.Features.V1.Rooms.GetRoomSchedule;

/// <param name="From">Start of the window.</param>
/// <param name="To">End of the window; at most 31 days after <paramref name="From"/>.</param>
public sealed record GetRoomScheduleQuery(DateTimeOffset? From, DateTimeOffset? To);