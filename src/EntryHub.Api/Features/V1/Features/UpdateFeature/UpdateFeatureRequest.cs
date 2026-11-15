using System.ComponentModel.DataAnnotations;

using EntryHub.Api.Domain.Features;

namespace EntryHub.Api.Features.V1.Features.UpdateFeature;

/// <summary>
/// Fields left out are not changed. The code never changes, and an entry is never deleted: a room may use it, so the
/// only way to retire it is <paramref name="IsActive"/> = <see langword="false"/>.
/// </summary>
/// <param name="Name">New name; unique among the entries of the tenant, ignoring case.</param>
/// <param name="Kind">New kind.</param>
/// <param name="IsActive"><see langword="false"/> stops new rooms from getting it; the rooms that have it keep it.</param>
public sealed record UpdateFeatureRequest(
    [property: StringLength(100, MinimumLength = 1)] string? Name,
    FeatureKind? Kind,
    bool? IsActive);