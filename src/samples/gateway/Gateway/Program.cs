using Gateway.RateLimiting;
using Gateway.Routing;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddRoutesFile();
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
builder.Services.AddPerClientRateLimit();

var app = builder.Build();

app.UseRateLimiter();
app.MapReverseProxy();

app.Run();