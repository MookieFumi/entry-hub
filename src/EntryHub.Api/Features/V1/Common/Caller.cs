using System.Security.Claims;

using EntryHub.Api.Security;

namespace EntryHub.Api.Features.V1.Common;

/// <summary>
/// Who is calling, taken from the token. Handlers receive this instead of the <see cref="ClaimsPrincipal"/>,
/// so they can be tested without HTTP.
/// </summary>
public sealed record Caller(string Id, bool IsFacilities, string? Email = null)
{
    public static Caller From(ClaimsPrincipal user) =>
        new(
            user.FindFirstValue("sub") ?? throw new InvalidOperationException("The token has no 'sub' claim."),
            user.IsInRole(Roles.Facilities),
            CleanEmail(user.FindFirstValue("email")));

    // Only to address a reminder, so anything that is not plausibly an address is dropped.
    private static string? CleanEmail(string? email) =>
        email?.Trim() is { Length: > 3 and <= 320 } clean && clean.Contains('@', StringComparison.Ordinal) && !clean.Contains(' ', StringComparison.Ordinal) ? clean : null;
}