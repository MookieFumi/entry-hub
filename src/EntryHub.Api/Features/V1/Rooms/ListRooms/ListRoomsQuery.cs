namespace EntryHub.Api.Features.V1.Rooms.ListRooms;

/// <param name="Floor">Only rooms on this floor.</param>
/// <param name="MinCapacity">Only rooms that fit at least this many people.</param>
/// <param name="Page">One-based page number; defaults to 1.</param>
/// <param name="PageSize">Items per page; defaults to 20 and is capped at 100.</param>
public sealed record ListRoomsQuery(int? Floor, int? MinCapacity, int? Page, int? PageSize);