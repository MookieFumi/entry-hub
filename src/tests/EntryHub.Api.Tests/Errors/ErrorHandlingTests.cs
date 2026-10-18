using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using EntryHub.Api.Data;
using EntryHub.Api.Security;
using EntryHub.Api.Tests.Support;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EntryHub.Api.Tests.Errors;

/// <summary>
/// What a client receives when a request fails on the server: problem details, never the exception.
/// </summary>
[Trait("Category", "Integration")]
public class ErrorHandlingTests
{
    [Fact]
    public async Task A_database_that_does_not_answer_returns_503_with_retry_after()
    {
        await using var factory = new ApiFactory(TestDatabase.Unreachable);
        using var client = ClientFor(factory);

        var response = await client.GetAsync("/api/v1/rooms", CancellationToken.None);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(TimeSpan.FromSeconds(5), response.Headers.RetryAfter?.Delta);

        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(CancellationToken.None);
        Assert.Equal(503, problem!.Status);
        Assert.Equal("Database unavailable", problem.Title);
        Assert.DoesNotContain("Npgsql", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unexpected_exception_returns_500_without_the_stack_trace()
    {
        await TestDatabase.EnsureMigratedAsync();
        await using var factory = new ApiFactory(TestDatabase.ConnectionString).WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<EntryHubDbContext>>();
                services.AddDbContext<EntryHubDbContext>(options => options
                    .UseNpgsql(TestDatabase.ConnectionString)
                    .AddInterceptors(new FailingCommandInterceptor()));
            }));
        using var client = ClientFor(factory);

        var response = await client.GetAsync("/api/v1/rooms", CancellationToken.None);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);
        Assert.DoesNotContain(FailingCommandInterceptor.Message, body, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(FailingCommandInterceptor), body, StringComparison.Ordinal);
    }

    private static HttpClient ClientFor(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokens.Create("ana", Roles.Employee));
        return client;
    }

    /// <summary>
    /// Makes every query fail with an exception no handler knows about, like a bug in a handler would.
    /// </summary>
    private sealed class FailingCommandInterceptor : DbCommandInterceptor
    {
        public const string Message = "Something nobody expected.";

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(Message);
    }
}