// Stands in for the old system: other URLs, upper-case field names and no versioning.
var app = WebApplication.CreateBuilder(args).Build();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Served-By"] = "legacy";
    await next();
});

object[] orders =
[
    new { ORDER_ID = 1, CUST_NAME = "Ada", TOTAL_AMT = 120.50m },
    new { ORDER_ID = 2, CUST_NAME = "Grace", TOTAL_AMT = 80.00m },
];

app.MapGet("/orders", () => orders);
app.MapGet("/customers/{id:int}", (int id) => new { CUST_ID = id, CUST_NAME = id == 1 ? "Ada" : "Grace" });

app.Run();