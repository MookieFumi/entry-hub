namespace EntryHub.Api.Tenancy;

/// <summary>
/// An entity that belongs to one tenant. The database context filters every query by the current tenant and
/// stamps the tenant on every row it inserts, so a handler never has to remember to do either.
/// </summary>
public interface ITenantOwned
{
    Guid TenantId { get; set; }
}