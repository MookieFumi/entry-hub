using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;

namespace EntryHub.Api.Cors;

public static class CorsExtensions
{
    public const string PolicyName = "Frontend";

    /// <summary>
    /// CORS for the browser front end. The allowed origins come from the <c>Cors:AllowedOrigins</c> setting and are read
    /// when the first request arrives. The API authenticates with a Bearer header, never with cookies, so credentials
    /// are not allowed and a stranger's page cannot ride on a signed-in browser.
    /// </summary>
    public static IServiceCollection AddEntryHubCors(this IServiceCollection services)
    {
        services.AddOptions<CorsSettings>().BindConfiguration("Cors");
        services.AddCors();
        services.AddOptions<CorsOptions>().Configure<IOptions<CorsSettings>>((cors, settings) =>
        {
            var origins = settings.Value.AllowedOrigins
                .Select(origin => origin.Trim().TrimEnd('/'))
                .Where(origin => origin.Length > 0)
                .ToArray();

            if (origins.Contains("*"))
            {
                throw new InvalidOperationException("Cors:AllowedOrigins must list the origins explicitly; '*' would let any page call the API.");
            }

            cors.AddPolicy(PolicyName, policy =>
            {
                if (origins.Length == 0)
                {
                    // No origins: the policy answers no CORS headers and the browser blocks every cross-origin call.
                    return;
                }

                policy
                    .WithOrigins(origins)
                    .SetIsOriginAllowedToAllowWildcardSubdomains()
                    .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
                    .WithHeaders("Authorization", "Content-Type")
                    .WithExposedHeaders("Retry-After", "Location")
                    .SetPreflightMaxAge(TimeSpan.FromHours(1));
            });
        });

        return services;
    }
}