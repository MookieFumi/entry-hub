using EntryHub.Api.Domain.Audit;

namespace EntryHub.Api.Features.V1.Audit;

/// <summary>
/// One change made through the API.
/// </summary>
/// <param name="Id">Identifier of the entry.</param>
/// <param name="At">When the change was saved.</param>
/// <param name="ActorId">Who made it (the <c>sub</c> of the token).</param>
/// <param name="Action"><c>Created</c>, <c>Updated</c>, <c>Deleted</c>, <c>Cancelled</c> or <c>Rescheduled</c>.</param>
/// <param name="EntityType"><c>Booking</c>, <c>Room</c>, <c>Site</c>, <c>Rules</c>, <c>Feature</c>, <c>SiteHoliday</c> or <c>RoomBlock</c>.</param>
/// <param name="EntityId">The thing that changed.</param>
/// <param name="Summary">The name of what was created, or the fields that changed (never their values).</param>
public sealed record AuditResponse(Guid Id, DateTimeOffset At, string ActorId, string Action, string EntityType, Guid EntityId, string? Summary)
{
    public static AuditResponse From(AuditEntry entry) => new(entry.Id, entry.At, entry.ActorId, entry.Action, entry.EntityType, entry.EntityId, entry.Summary);
}

/// <param name="EntityType">Only changes to this type of thing.</param>
/// <param name="EntityId">Only changes to this thing.</param>
/// <param name="ActorId">Only changes made by this person.</param>
/// <param name="From">Only changes at or after this moment.</param>
/// <param name="To">Only changes before this moment.</param>
/// <param name="Page">One-based page number; defaults to 1.</param>
/// <param name="PageSize">Items per page; defaults to 20 and is capped at 100.</param>
public sealed record ListAuditQuery(string? EntityType, Guid? EntityId, string? ActorId, DateTimeOffset? From, DateTimeOffset? To, int? Page, int? PageSize);