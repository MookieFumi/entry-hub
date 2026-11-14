using System.Security.Claims;

namespace EntryHub.Api.Tenancy;

/// <summary>
/// The tenant of the current request.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// <see langword="null"/> when there is no tenant, which makes every query return nothing.
    /// </summary>
    Guid? TenantId { get; }
}

public static class TenantClaims
{
    /// <summary>
    /// The claim that names the tenant. It is the only source of the tenant: never a header, a route or a body.
    /// </summary>
    public const string TenantId = "tenant_id";

    public static Guid? Read(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(TenantId), out var id) && id != Guid.Empty ? id : null;
}

/// <summary>
/// Takes the tenant from the signed token of the request.
/// </summary>
public sealed class HttpTenantContext(IHttpContextAccessor accessor) : ITenantContext
{
    public Guid? TenantId => accessor.HttpContext?.User is { } user ? TenantClaims.Read(user) : null;
}

/// <summary>
/// A fixed tenant, for code that runs outside a request: tests and tools.
/// </summary>
public sealed class FixedTenantContext(Guid? tenantId) : ITenantContext
{
    public Guid? TenantId { get; } = tenantId;
}