// Stands in for the new API: versioned URLs and camelCase fields.
var app = WebApplication.CreateBuilder(args).Build();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Served-By"] = "new";
    await next();
});

app.MapGet("/api/v1/orders", () => new[]
{
    new Order(1, "Ada", 120.50m),
    new Order(2, "Grace", 80.00m),
});

app.MapGet("/api/v1/customers/{id:int}", (int id) => new Customer(id, id == 1 ? "Ada" : "Grace"));

app.Run();

internal sealed record Order(int Id, string Customer, decimal Total);

internal sealed record Customer(int Id, string Name);