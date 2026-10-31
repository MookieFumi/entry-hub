namespace NewApi.Customers;

internal static class CustomersEndpoints
{
    public static void MapCustomers(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/customers/{id:int}", (int id) => new Customer(id, id == 1 ? "Ada" : "Grace"));
    }
}