using System.Text.RegularExpressions;

using EntryHub.Api.Tenancy;

namespace EntryHub.Api.Domain.Features;

/// <summary>
/// An entry of the catalog of a tenant: something a room can have, such as a projector or wheelchair access.
/// Entries are never deleted, only deactivated, so a room never points at something that vanished.
/// </summary>
public sealed partial class Feature : ITenantOwned
{
    public Guid Id { get; init; }

    public Guid TenantId { get; set; }

    /// <summary>
    /// Stable lowercase identifier, such as <c>video-conferencing</c>. Filters use it; it never changes.
    /// </summary>
    public required string Code { get; init; }

    public required string Name { get; set; }

    public FeatureKind Kind { get; set; }

    /// <summary>
    /// A deactivated entry cannot be given to a room any more; the rooms that have it keep it.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// The code for a name: lowercase letters and digits separated by single hyphens.
    /// </summary>
    public static string CodeFor(string name) => NotAlphanumeric().Replace(name.Trim().ToLowerInvariant(), "-").Trim('-');

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NotAlphanumeric();
}