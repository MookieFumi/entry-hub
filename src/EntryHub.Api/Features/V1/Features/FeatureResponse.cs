using EntryHub.Api.Domain.Features;

namespace EntryHub.Api.Features.V1.Features;

/// <summary>
/// An entry of the catalog of the tenant.
/// </summary>
/// <param name="Id">Identifier to give to a room.</param>
/// <param name="Code">Stable lowercase identifier, such as <c>video-conferencing</c>; the <c>features</c> filter uses it.</param>
/// <param name="Name">Name to show.</param>
/// <param name="Kind">Equipment or accessibility.</param>
/// <param name="IsActive">A deactivated entry cannot be given to a room; the rooms that have it keep it.</param>
public sealed record FeatureResponse(Guid Id, string Code, string Name, FeatureKind Kind, bool IsActive)
{
    public static FeatureResponse From(Feature feature) => new(feature.Id, feature.Code, feature.Name, feature.Kind, feature.IsActive);
}