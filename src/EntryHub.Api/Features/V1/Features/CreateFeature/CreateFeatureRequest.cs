using System.ComponentModel.DataAnnotations;

using EntryHub.Api.Domain.Features;

namespace EntryHub.Api.Features.V1.Features.CreateFeature;

/// <param name="Name">Name to show; unique among the entries of the tenant, ignoring case.</param>
/// <param name="Kind">Equipment or accessibility.</param>
public sealed record CreateFeatureRequest(
    [property: Required, StringLength(100, MinimumLength = 1)] string Name,
    FeatureKind Kind);