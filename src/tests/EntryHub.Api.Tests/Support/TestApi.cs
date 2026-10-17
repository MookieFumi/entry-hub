using System.Net.Http.Headers;

namespace EntryHub.Api.Tests.Support;

/// <summary>
/// The API hosted in memory over the real database, plus helpers to call it as different people.
/// </summary>
internal sealed class TestApi : IDisposable
{
    private readonly ApiFactory _factory = new(TestDatabase.ConnectionString);

    private TestApi()
    {
    }

    public static async Task<TestApi> StartAsync()
    {
        await TestDatabase.EnsureMigratedAsync();
        return new TestApi();
    }

    public HttpClient Anonymous() => _factory.CreateClient();

    public HttpClient ClientFor(string subject, params string[] roles)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Create(subject, roles));
        return client;
    }

    public void Dispose() => _factory.Dispose();
}