namespace EntryHub.Api.Features.V1.Features.ListFeatures;

/// <param name="IncludeInactive">Also return deactivated entries. Only Facilities get them; for anyone else it is ignored.</param>
public sealed record ListFeaturesQuery(bool? IncludeInactive);