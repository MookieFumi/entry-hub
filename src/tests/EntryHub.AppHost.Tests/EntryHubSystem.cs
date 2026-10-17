using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace EntryHub.AppHost.Tests;

/// <summary>
/// The whole system as the AppHost describes it: PostgreSQL in a container, the API and the reminders service.
/// Started once and shared by the tests of the collection, because starting it takes the longest.
/// </summary>
public sealed class EntryHubSystem : IAsyncLifetime
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(3);

    private DistributedApplication? _app;

    public DistributedApplication App => _app ?? throw new InvalidOperationException("The system is not started.");

    public async Task InitializeAsync()
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.EntryHub_AppHost>();

        // Same wiring as production, plus the settings that make the API accept the tokens of these tests.
        var api = builder.CreateResourceBuilder<ProjectResource>("api");
        foreach (var (name, value) in Tokens.ApiSettings)
        {
            api.WithEnvironment(name, value);
        }

        _app = await builder.BuildAsync();
        await _app.StartAsync();

        using var timeout = new CancellationTokenSource(StartupTimeout);
        await _app.ResourceNotifications.WaitForResourceHealthyAsync("api", timeout.Token);
        await _app.ResourceNotifications.WaitForResourceAsync("reminders", KnownResourceStates.Running, timeout.Token);
    }

    public HttpClient CreateApiClient(string subject, params string[] roles)
    {
        var client = App.CreateHttpClient("api");
        client.DefaultRequestHeaders.Authorization = new("Bearer", Tokens.Create(subject, roles));
        return client;
    }

    public Task<string?> GetConnectionStringAsync(CancellationToken cancellationToken) =>
        App.GetConnectionStringAsync("EntryHub", cancellationToken).AsTask();

    public async Task DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}

[CollectionDefinition(Name)]
public sealed class SystemCollectionDefinition : ICollectionFixture<EntryHubSystem>
{
    public const string Name = "EntryHub system";
}