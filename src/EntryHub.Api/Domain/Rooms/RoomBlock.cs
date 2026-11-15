using EntryHub.Api.Tenancy;

namespace EntryHub.Api.Domain.Rooms;

/// <summary>
/// A time range in which a room cannot be booked (cleaning, repairs). Like a booking it includes
/// <see cref="StartAt"/> and excludes <see cref="EndAt"/>.
/// </summary>
public sealed class RoomBlock : ITenantOwned
{
    public Guid Id { get; init; }

    public Guid TenantId { get; set; }

    public Guid RoomId { get; init; }

    public DateTimeOffset StartAt { get; init; }

    public DateTimeOffset EndAt { get; init; }

    public string? Reason { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}