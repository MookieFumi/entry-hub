using System.Security.Claims;
using System.Threading.RateLimiting;

using EntryHub.Api.Tenancy;

using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace EntryHub.Api.RateLimiting;

public static class RateLimitingExtensions
{
    private static readonly string[] ProbePaths = ["/health", "/alive"];

    /// <summary>
    /// A fixed-window limiter for every request: per tenant for callers with a token, per IP address for the rest.
    /// The probes of the platform are never limited, because an orchestrator must be able to ask them at any rate.
    /// </summary>
    public static IServiceCollection AddEntryHubRateLimiting(this IServiceCollection services)
    {
        services.AddOptions<RateLimitingSettings>().BindConfiguration("RateLimiting");

        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>().Configure<IOptions<RateLimitingSettings>>((limiter, settings) =>
        {
            var rules = settings.Value;
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context => Partition(context, rules));
            limiter.OnRejected = WriteRejectionAsync;
        });

        return services;
    }

    private static RateLimitPartition<string> Partition(HttpContext context, RateLimitingSettings rules)
    {
        if (!rules.Enabled || ProbePaths.Contains(context.Request.Path.Value, StringComparer.OrdinalIgnoreCase))
        {
            return RateLimitPartition.GetNoLimiter("unlimited");
        }

        var (key, limit) = Identify(context, rules);

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limit,
            Window = TimeSpan.FromSeconds(Math.Max(1, rules.WindowSeconds)),
            QueueLimit = 0,
        });
    }

    private static (string Key, int Limit) Identify(HttpContext context, RateLimitingSettings rules)
    {
        if (TenantClaims.Read(context.User) is { } tenantId)
        {
            return ($"tenant:{tenantId}", rules.PermitLimit);
        }

        if (context.User.FindFirstValue("sub") is { Length: > 0 } subject)
        {
            return ($"user:{subject}", rules.PermitLimit);
        }

        return ($"ip:{context.Connection.RemoteIpAddress}", rules.AnonymousPermitLimit);
    }

    private static async ValueTask WriteRejectionAsync(OnRejectedContext rejection, CancellationToken cancellationToken)
    {
        var context = rejection.HttpContext;
        var window = context.RequestServices.GetRequiredService<IOptions<RateLimitingSettings>>().Value.WindowSeconds;
        var retryAfter = rejection.Lease.TryGetMetadata(MetadataName.RetryAfter, out var wait)
            ? (int)Math.Ceiling(wait.TotalSeconds)
            : window;

        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers.RetryAfter = Math.Max(1, retryAfter).ToString(System.Globalization.CultureInfo.InvariantCulture);

        await context.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails =
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too many requests",
                Detail = "Too many requests were sent in a short time. Wait for the time in Retry-After and try again.",
            },
        });
    }
}