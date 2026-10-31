using NewApi.Customers;
using NewApi.Infrastructure;
using NewApi.Orders;

// Stands in for the new API: versioned URLs and camelCase fields.
var app = WebApplication.CreateBuilder(args).Build();

app.UseServedBy("new");
app.MapOrders();
app.MapCustomers();

app.Run();