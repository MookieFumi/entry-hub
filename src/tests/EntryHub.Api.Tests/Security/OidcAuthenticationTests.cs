using System.Net;
using System.Net.Http.Json;

using EntryHub.Api.Features.V1.Rooms.CreateRoom;
using EntryHub.Api.Security;
using EntryHub.Api.Tests.Support;

namespace EntryHub.Api.Tests.Security;

/// <summary>
/// The API as it runs on the demo: it trusts an OpenID Connect provider (Supabase Auth) through its public keys and
/// reads the application role from <c>user_role</c>, because <c>role</c> holds the PostgreSQL role of the provider.
/// </summary>
[Trait("Category", "Integration")]
public class OidcAuthenticationTests : IAsyncLifetime
{
    private FakeOidcProvider _provider = null!;

    public async Task InitializeAsync() => _provider = await FakeOidcProvider.StartAsync();

    public async Task DisposeAsync() => await _provider.DisposeAsync();

    private static string UniqueName() => $"Room {Guid.NewGuid():N}";

    [Fact]
    public async Task A_token_from_the_provider_is_accepted_and_its_user_role_decides_what_it_can_do()
    {
        using var api = await TestApi.StartAsync(_provider.ApiConfiguration);
        using var facilities = api.ClientWithToken(_provider.CreateToken($"fran-{Guid.NewGuid():N}", Roles.Facilities, TestTenants.Default));
        using var employee = api.ClientWithToken(_provider.CreateToken($"ana-{Guid.NewGuid():N}", Roles.Employee, TestTenants.Default));

        var created = await facilities.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(UniqueName(), 1, 4, null));
        var employeeCreates = await employee.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(UniqueName(), 1, 4, null));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await employee.GetAsync("/api/v1/rooms")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, employeeCreates.StatusCode);
    }

    [Fact]
    public async Task The_role_claim_of_the_provider_grants_nothing()
    {
        using var api = await TestApi.StartAsync(_provider.ApiConfiguration);

        // role is "authenticated" or even "Facilities" here, but there is no user_role: the person has no application role.
        using var plain = api.ClientWithToken(_provider.CreateToken("eve", userRole: null, TestTenants.Default));
        using var spoofed = api.ClientWithToken(_provider.CreateToken("eve", userRole: null, TestTenants.Default, role: Roles.Facilities));

        Assert.Equal(HttpStatusCode.Forbidden, (await plain.GetAsync("/api/v1/rooms")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await spoofed.GetAsync("/api/v1/rooms")).StatusCode);
    }

    [Fact]
    public async Task A_token_from_the_provider_without_a_tenant_gets_403()
    {
        using var api = await TestApi.StartAsync(_provider.ApiConfiguration);
        using var client = api.ClientWithToken(_provider.CreateToken("fran", Roles.Facilities, tenantId: null));

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/rooms")).StatusCode);
    }

    [Fact]
    public async Task A_token_signed_with_a_key_the_provider_never_published_is_rejected()
    {
        using var api = await TestApi.StartAsync(_provider.ApiConfiguration);
        using var client = api.ClientWithToken(_provider.CreateToken("fran", Roles.Facilities, TestTenants.Default, signWith: FakeOidcProvider.UnknownKey()));

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/rooms")).StatusCode);
    }

    [Fact]
    public async Task Tokens_signed_with_the_old_shared_key_are_no_longer_accepted_when_an_authority_is_set()
    {
        using var api = await TestApi.StartAsync(_provider.ApiConfiguration);
        using var client = api.ClientFor("fran", Roles.Facilities);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/rooms")).StatusCode);
    }
}