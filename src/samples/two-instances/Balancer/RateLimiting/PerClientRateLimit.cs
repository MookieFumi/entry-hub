using System.Threading.RateLimiting;

namespace Balancer.RateLimiting;

internal static class PerClientRateLimit
{
    // One limit per API key counted in the balancer, so it holds however many instances sit behind it.
    public static void AddPerClientRateLimit(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("per-client", context =>
            {
                var key = context.Request.Headers["X-Api-Key"].FirstOrDefault() ?? "anonymous";
                return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 20,
                    Window = TimeSpan.FromMinutes(1),
                });
            });
        });
    }
}