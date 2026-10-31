using System.Threading.RateLimiting;

namespace Gateway.RateLimiting;

internal static class PerClientRateLimit
{
    public const string PolicyName = "per-client";

    // One limit per API key, counted in the gateway: the services behind it never see the excess.
    public static void AddPerClientRateLimit(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(PolicyName, context =>
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