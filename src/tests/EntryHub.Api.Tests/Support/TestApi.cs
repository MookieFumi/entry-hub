using System.Net.Http.Headers;

namespace EntryHub.Api.Tests.Support;

/// <summary>
/// The API hosted in memory over the real database, plus helpers to call it as different people.
/// </summary>
internal sealed class TestApi : IDisposable
{
    private readonly ApiFactory _factory;

    private TestApi(IReadOnlyDictionary<string, string?>? authentication) =>
        _factory = new ApiFactory(TestDatabase.ConnectionString, authentication);

    /// <param name="authentication">How the API validates tokens; the signing key of <see cref="TestTokens"/> by default.</param>
    public static async Task<TestApi> StartAsync(IReadOnlyDictionary<string, string?>? authentication = null)
    {
        await TestDatabase.EnsureMigratedAsync();
        return new TestApi(authentication);
    }

    public HttpClient Anonymous() => _factory.CreateClient();

    public HttpClient ClientFor(string subject, params string[] roles)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Create(subject, roles));
        return client;
    }

    public HttpClient ClientForTenant(Guid tenantId, string subject, params string[] roles) =>
        ClientWithToken(TestTokens.CreateForTenant(tenantId, subject, roles));

    public HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public void Dispose() => _factory.Dispose();
}