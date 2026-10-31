using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// The routes live in their own file so the migration is a change to that file. The proxy reloads it without restarting.
var routesFile = builder.Configuration["Gateway:RoutesFile"] ?? "routes.step1.json";
builder.Configuration.AddJsonFile(routesFile, optional: false, reloadOnChange: true);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// One limit per API key, counted in the gateway: the services behind it never see the excess.
builder.Services.AddRateLimiter(options =>
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

var app = builder.Build();

app.UseRateLimiter();
app.MapReverseProxy();

app.Run();