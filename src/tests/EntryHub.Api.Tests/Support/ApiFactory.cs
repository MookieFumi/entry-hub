using EntryHub.Api.Data;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EntryHub.Api.Tests.Support;

/// <summary>
/// Hosts the API in memory against the PostgreSQL given by <paramref name="connectionString"/>.
/// </summary>
internal sealed class ApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<EntryHubDbContext>>();
            services.AddDbContext<EntryHubDbContext>(options => options.UseNpgsql(connectionString));
        });
}