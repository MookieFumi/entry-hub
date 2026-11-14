using System.Net;
using System.Security.Cryptography;

using EntryHub.Api.Security;
using EntryHub.Api.Tests.Support;

using Microsoft.IdentityModel.Tokens;

namespace EntryHub.Api.Tests.Security;

/// <summary>
/// The API only accepts tokens it can trust: signed with its key, for its audience, from its issuer and still valid.
/// Every other token must get a 401, whatever its claims say.
/// </summary>
public class TokenValidationTests
{
    private const string ProtectedPath = "/api/v1/bookings/mine";

    public static TheoryData<string> InvalidTokens => [.. InvalidTokenFactories.Keys];

    private static readonly Dictionary<string, Func<SecurityTokenDescriptor, SecurityTokenDescriptor>> InvalidTokenFactories = new()
    {
        ["Unsigned (alg none)"] = descriptor =>
        {
            descriptor.SigningCredentials = null;
            return descriptor;
        },
        ["Signed with another key"] = descriptor =>
        {
            var otherKey = new SymmetricSecurityKey(SHA256.HashData("someone-else-signing-key"u8));
            descriptor.SigningCredentials = new SigningCredentials(otherKey, SecurityAlgorithms.HmacSha256);
            return descriptor;
        },
        ["Expired one minute ago"] = descriptor => ExpiredAgo(descriptor, TimeSpan.FromMinutes(1)),
        ["For another audience"] = descriptor =>
        {
            descriptor.Audience = "another-api";
            return descriptor;
        },
        ["From another issuer"] = descriptor =>
        {
            descriptor.Issuer = "another-issuer";
            return descriptor;
        },
    };

    [Fact]
    public async Task A_valid_token_is_accepted()
    {
        using var api = await TestApi.StartAsync();
        using var client = api.ClientWithToken(TestTokens.Sign(Employee()));

        var response = await client.GetAsync(ProtectedPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(InvalidTokens))]
    public async Task An_invalid_token_is_rejected(string invalidToken)
    {
        using var api = await TestApi.StartAsync();
        var token = TestTokens.Sign(InvalidTokenFactories[invalidToken](Employee()));
        using var client = api.ClientWithToken(token);

        var response = await client.GetAsync(ProtectedPath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_token_that_expired_within_the_clock_skew_is_still_accepted()
    {
        using var api = await TestApi.StartAsync();
        using var client = api.ClientWithToken(TestTokens.Sign(ExpiredAgo(Employee(), TimeSpan.FromSeconds(10))));

        var response = await client.GetAsync(ProtectedPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static SecurityTokenDescriptor Employee() => TestTokens.Describe($"ana-{Guid.NewGuid():N}", Roles.Employee);

    private static SecurityTokenDescriptor ExpiredAgo(SecurityTokenDescriptor descriptor, TimeSpan ago)
    {
        var expires = DateTime.UtcNow - ago;
        descriptor.IssuedAt = expires.AddHours(-1);
        descriptor.NotBefore = expires.AddHours(-1);
        descriptor.Expires = expires;
        return descriptor;
    }
}