namespace EntryHub.Api.Features.V1.Rooms.SearchAvailability;

/// <param name="From">Start of the slot to book.</param>
/// <param name="To">End of the slot to book.</param>
/// <param name="Attendees">People that must fit; the room needs at least this capacity.</param>
/// <param name="MinCapacity">Only rooms that fit at least this many people.</param>
/// <param name="Floor">Only rooms on this floor.</param>
/// <param name="Features">Codes from <c>GET /features</c> separated by commas; only rooms that have all of them.</param>
/// <param name="SiteId">Only rooms of this site.</param>
public sealed record SearchAvailabilityQuery(DateTimeOffset? From, DateTimeOffset? To, int? Attendees, int? MinCapacity, int? Floor, Guid? SiteId = null, string? Features = null);