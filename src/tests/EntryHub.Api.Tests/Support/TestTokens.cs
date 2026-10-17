using System.Security.Claims;
using System.Security.Cryptography;

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace EntryHub.Api.Tests.Support;

/// <summary>
/// Signs tokens the same way <c>dotnet user-jwts</c> does, with a key that only exists in the tests.
/// </summary>
internal static class TestTokens
{
    private const string Issuer = "entryhub-tests";
    private const string Audience = "entryhub-api";

    private static readonly byte[] Key = SHA256.HashData("entryhub-tests-signing-key"u8);

    /// <summary>
    /// The configuration the API reads to validate those tokens.
    /// </summary>
    public static Dictionary<string, string?> Configuration { get; } = new()
    {
        ["Authentication:Schemes:Bearer:ValidIssuer"] = Issuer,
        ["Authentication:Schemes:Bearer:ValidAudiences:0"] = Audience,
        ["Authentication:Schemes:Bearer:SigningKeys:0:Issuer"] = Issuer,
        ["Authentication:Schemes:Bearer:SigningKeys:0:Value"] = Convert.ToBase64String(Key),
        ["Authentication:Schemes:Bearer:SigningKeys:0:Length"] = Key.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
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