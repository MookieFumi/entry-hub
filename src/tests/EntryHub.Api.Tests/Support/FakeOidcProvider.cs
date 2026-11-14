using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Security.Cryptography;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace EntryHub.Api.Tests.Support;

/// <summary>
/// An OpenID Connect provider with one RSA key, shaped like Supabase Auth: the issuer ends in <c>/auth/v1</c>, the public
/// keys are a JWKS, and the tokens carry <c>role = authenticated</c> next to the application role in <c>user_role</c>.
/// </summary>
internal sealed class FakeOidcProvider : IAsyncDisposable
{
    public const string Audience = "authenticated";

    private const string KeyId = "test-key";

    private readonly WebApplication _app;
    private readonly RsaSecurityKey _key;

    private FakeOidcProvider(WebApplication app, string authority, RsaSecurityKey key)
    {
        _app = app;
        Authority = authority;
        _key = key;
    }

    public string Authority { get; }

    /// <summary>
    /// The settings that point the API at this provider, with the application role in <c>user_role</c>.
    /// </summary>
    public IReadOnlyDictionary<string, string?> ApiConfiguration => new Dictionary<string, string?>
    {
        ["Authentication:Schemes:Bearer:Authority"] = Authority,
        ["Authentication:Schemes:Bearer:ValidAudiences:0"] = Audience,
        ["Authentication:RoleClaim"] = "user_role",
    };

    public static async Task<FakeOidcProvider> StartAsync()
    {
        var rsa = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa) { KeyId = KeyId };
        var publicKey = rsa.ExportParameters(false);

        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();

        var authority = new StrongBox<string>(string.Empty);

        app.MapGet("/auth/v1/.well-known/openid-configuration", () => Results.Json(new
        {
            issuer = authority.Value,
            jwks_uri = $"{authority.Value}/.well-known/jwks.json",
        }));
        app.MapGet("/auth/v1/.well-known/jwks.json", () => Results.Json(new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA",
                    kid = KeyId,
                    use = "sig",
                    alg = SecurityAlgorithms.RsaSha256,
                    n = Base64UrlEncoder.Encode(publicKey.Modulus),
                    e = Base64UrlEncoder.Encode(publicKey.Exponent),
                },
            },
        }));

        await app.StartAsync();
        authority.Value = $"{app.Urls.First()}/auth/v1";
        return new FakeOidcProvider(app, authority.Value, key);
    }

    /// <summary>
    /// A token like the ones Supabase issues after the token hook ran: <c>role</c> is the PostgreSQL role, the application
    /// role is in <paramref name="userRole"/> and the tenant in <c>tenant_id</c>. A <see langword="null"/> leaves it out.
    /// </summary>
    public string CreateToken(string subject, string? userRole, Guid? tenantId, string role = "authenticated", RsaSecurityKey? signWith = null)
    {
        var claims = new List<Claim> { new("sub", subject), new("role", role) };
        if (userRole is not null)
        {
            claims.Add(new Claim("user_role", userRole));
        }

        if (tenantId is { } tenant)
        {
            claims.Add(new Claim("tenant_id", tenant.ToString()));
        }

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = Authority,
            Audience = Audience,
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(signWith ?? _key, SecurityAlgorithms.RsaSha256),
        });
    }

    /// <summary>
    /// A key the provider never published, with the same key id as the real one.
    /// </summary>
    public static RsaSecurityKey UnknownKey() => new(RSA.Create(2048)) { KeyId = KeyId };

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();
}