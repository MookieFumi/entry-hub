using System.Security.Claims;
using System.Security.Cryptography;

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace EntryHub.AppHost.Tests;

/// <summary>
/// Signs tokens with a key that only exists in these tests and hands the API the settings to accept them.
/// The API runs as a separate process, so the settings travel as environment variables of its resource.
/// </summary>
internal static class Tokens
{
    private const string Issuer = "entryhub-aspire-tests";
    private const string Audience = "entryhub-api";

    private static readonly byte[] Key = SHA256.HashData("entryhub-aspire-tests-signing-key"u8);

    public static IReadOnlyDictionary<string, string> ApiSettings { get; } = new Dictionary<string, string>
    {
        ["Authentication__Schemes__Bearer__ValidIssuer"] = Issuer,
        ["Authentication__Schemes__Bearer__ValidAudiences__0"] = Audience,
        ["Authentication__Schemes__Bearer__SigningKeys__0__Issuer"] = Issuer,
        ["Authentication__Schemes__Bearer__SigningKeys__0__Value"] = Convert.ToBase64String(Key),
        ["Authentication__Schemes__Bearer__SigningKeys__0__Length"] = Key.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    public static string Create(string subject, params string[] roles)
    {
        var claims = new List<Claim> { new("sub", subject) };
        claims.AddRange(roles.Select(role => new Claim("role", role)));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = Issuer,
            Audience = Audience,
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Key), SecurityAlgorithms.HmacSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}