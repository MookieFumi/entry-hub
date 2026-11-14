using EntryHub.Api.Data;
using EntryHub.Api.Features.V1.Rooms.StreamRoomOccupancy;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EntryHub.Api.Tests.Support;

/// <summary>
/// Hosts the API in memory against the PostgreSQL given by <paramref name="connectionString"/>. It accepts the tokens
/// of <see cref="TestTokens"/> unless <paramref name="authentication"/> says how else to validate them.
/// </summary>
internal sealed class ApiFactory(string connectionString, IReadOnlyDictionary<string, string?>? authentication = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseEnvironment("Testing")
            .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(authentication ?? TestTokens.Configuration))
            .ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(TestClock.Default);
            services.RemoveAll<DbContextOptions<EntryHubDbContext>>();
            services.AddDbContext<EntryHubDbContext>(options => options.UseNpgsql(connectionString));
            // The fake identity provider of the OIDC tests speaks plain HTTP.
            services.Configure<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>("Bearer", options => options.RequireHttpsMetadata = false);
            services.Configure<StreamRoomOccupancyOptions>(options => options.Interval = TimeSpan.FromMilliseconds(50));
        });
}