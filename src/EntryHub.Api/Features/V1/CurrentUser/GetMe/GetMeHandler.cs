using System.Security.Claims;

using EntryHub.Api.Data;
using EntryHub.Api.Security;
using EntryHub.Api.Tenancy;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Features.V1.CurrentUser.GetMe;

public sealed class GetMeHandler(EntryHubDbContext db)
{
    /// <returns>Who is calling, or <see langword="null"/> when the tenant of the token does not exist.</returns>
    public async Task<MeResponse?> HandleAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var tenantId = TenantClaims.Read(user);
        var userId = user.FindFirstValue("sub");

        if (tenantId is null || userId is null)
        {
            return null;
        }

        // The tenants table is the list of customers and has no tenant filter: a person may read their own row only.
        var name = await db.Tenants.AsNoTracking()
            .Where(tenant => tenant.Id == tenantId)
            .Select(tenant => tenant.Name)
            .SingleOrDefaultAsync(cancellationToken);

        return name is null
            ? null
            : new MeResponse(userId, user.IsInRole(Roles.Facilities) ? Roles.Facilities : Roles.Employee, tenantId.Value, name);
    }
}