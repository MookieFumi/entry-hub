namespace EntryHub.Api.Features.V1.Sites.ListSites;

/// <param name="IncludeInactive">Also return deactivated sites. Only Facilities get them; for anyone else it is ignored.</param>
public sealed record ListSitesQuery(bool? IncludeInactive);