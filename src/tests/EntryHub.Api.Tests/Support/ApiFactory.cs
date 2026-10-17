using EntryHub.Api.Data;
using EntryHub.Api.Features.V1.Rooms.StreamRoomOccupancy;

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
        builder.UseEnvironment("Testing").ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<EntryHubDbContext>>();
            services.AddDbContext<EntryHubDbContext>(options => options.UseNpgsql(connectionString));
            services.Configure<StreamRoomOccupancyOptions>(options => options.Interval = TimeSpan.FromMilliseconds(50));
        });
}