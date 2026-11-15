namespace EntryHub.Api.Features.V1.Rooms.ListRooms;

/// <param name="Floor">Only rooms on this floor.</param>
/// <param name="MinCapacity">Only rooms that fit at least this many people.</param>
/// <param name="IncludeInactive">Also return deactivated rooms. Only Facilities get them; for anyone else it is ignored.</param>
/// <param name="SiteId">Only rooms of this site.</param>
/// <param name="Features">Codes from <c>GET /features</c> separated by commas; only rooms that have all of them.</param>
/// <param name="Page">One-based page number; defaults to 1.</param>
/// <param name="PageSize">Items per page; defaults to 20 and is capped at 100.</param>
public sealed record ListRoomsQuery(int? Floor, int? MinCapacity, bool? IncludeInactive, int? Page, int? PageSize, Guid? SiteId = null, string? Features = null);