namespace EntryHub.Api.Features.V1.Rooms.StreamRoomOccupancy;

public sealed class StreamRoomOccupancyOptions
{
    /// <summary>
    /// Time between two snapshots sent to a connected client.
    /// </summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(5);
}