using System.Threading.RateLimiting;

namespace ProductsApi.RateLimiting;

internal static class PerInstanceRateLimit
{
    // The middleware keeps its counters in the memory of each instance: with N instances behind a balancer, the
    // effective limit is N times the configured one. RateLimit:Enabled=false turns it off.
    public static void AddPerInstanceRateLimit(this IServiceCollection services, IConfiguration configuration)
    {
        var enabled = configuration.GetValue("RateLimit:Enabled", true);
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("per-instance", context =>
            {
                if (!enabled)
                {
                    return RateLimitPartition.GetNoLimiter("off");
                }

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