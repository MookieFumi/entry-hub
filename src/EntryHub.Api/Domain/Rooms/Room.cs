namespace EntryHub.Api.Domain.Rooms;

/// <summary>
/// A meeting room that employees can book.
/// </summary>
public sealed class Room
{
    public Guid Id { get; init; }

    public required string Name { get; set; }

    public int Floor { get; set; }

    public int Capacity { get; set; }

    public List<string> Equipment { get; set; } = [];

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; init; }
}