using EntryHub.Api.Tenancy;

namespace EntryHub.Api.Domain.Audit;

/// <summary>
/// One change made through the API, written in the same transaction as the change. Entries are only ever added.
/// </summary>
public sealed class AuditEntry : ITenantOwned
{
    public Guid Id { get; init; }

    public Guid TenantId { get; set; }

    public DateTimeOffset At { get; init; }

    /// <summary>
    /// The <c>sub</c> claim of whoever made the change.
    /// </summary>
    public required string ActorId { get; init; }

    /// <summary>
    /// <c>Created</c>, <c>Updated</c>, <c>Deleted</c>, <c>Cancelled</c>, <c>Rescheduled</c> or <c>Exported</c>.
    /// </summary>
    public required string Action { get; init; }

    /// <summary>
    /// <c>Booking</c>, <c>Room</c>, <c>Site</c>, <c>Rules</c>, <c>Feature</c>, <c>SiteHoliday</c>, <c>RoomBlock</c> or <c>Tenant</c>.
    /// </summary>
    public required string EntityType { get; init; }

    public Guid EntityId { get; init; }

    /// <summary>
    /// A short description: the name of what was created, or the fields that changed. Never the values, so the
    /// trail holds no personal data of its own.
    /// </summary>
    public string? Summary { get; init; }
}