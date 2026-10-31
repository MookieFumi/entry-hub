using LegacyApi.Customers;
using LegacyApi.Infrastructure;
using LegacyApi.Orders;

// Stands in for the old system: other URLs, upper-case field names and no versioning.
var app = WebApplication.CreateBuilder(args).Build();

app.UseServedBy("legacy");
app.MapOrders();
app.MapCustomers();

app.Run();