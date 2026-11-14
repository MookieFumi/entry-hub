using System.Security.Claims;

using EntryHub.Api.Features.V1.Common;
using EntryHub.Api.Security;

namespace EntryHub.Api.Tests.Support;

/// <summary>
/// Builds the <see cref="ClaimsPrincipal"/> that the API would get from a token, for handlers that need one.
/// </summary>
internal static class TestUsers
{
    public static ClaimsPrincipal PrincipalFor(Caller caller)
    {
        var claims = new List<Claim> { new("sub", caller.Id), new("role", Roles.Employee) };

        if (caller.IsFacilities)
        {
            claims.Add(new Claim("role", Roles.Facilities));
        }

        // Same claim names as the JWT bearer setup: "sub" for the name and "role" for roles.
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test", "sub", "role"));
    }
}