using EntryHub.Api.Security;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace EntryHub.Api.Tests.Support;

/// <summary>
/// The authorization service with the same policies and handlers that the API registers.
/// </summary>
internal static class TestAuthorization
{
    private static readonly ServiceProvider Services = new ServiceCollection()
        .AddLogging()
        .AddEntryHubSecurity()
        .BuildServiceProvider();

    public static IAuthorizationService Service { get; } = Services.GetRequiredService<IAuthorizationService>();
}