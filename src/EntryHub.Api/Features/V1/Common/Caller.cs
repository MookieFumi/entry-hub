using System.Security.Claims;

using EntryHub.Api.Security;

namespace EntryHub.Api.Features.V1.Common;

/// <summary>
/// Who is calling, taken from the token. Handlers receive this instead of the <see cref="ClaimsPrincipal"/>,
/// so they can be tested without HTTP.
/// </summary>
public sealed record Caller(string Id, bool IsFacilities)
{
    public static Caller From(ClaimsPrincipal user) =>
        new(
            user.FindFirstValue("sub") ?? throw new InvalidOperationException("The token has no 'sub' claim."),
            user.IsInRole(Roles.Facilities));
}