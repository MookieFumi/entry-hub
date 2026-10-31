using ProductsApi.Caching;
using ProductsApi.Infrastructure;
using ProductsApi.Products;
using ProductsApi.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ProductStore>();
builder.Services.AddProductCache(builder.Configuration);
builder.Services.AddPerInstanceRateLimit(builder.Configuration);

var app = builder.Build();

app.UseServedBy(builder.Configuration["Instance:Name"] ?? "instance");
app.UseRateLimiter();
app.MapProducts();

app.Run();