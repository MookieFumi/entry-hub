namespace LegacyApi.Customers;

internal static class CustomersEndpoints
{
    public static void MapCustomers(this IEndpointRouteBuilder app)
    {
        app.MapGet("/customers/{id:int}", (int id) => new LegacyCustomer(id, id == 1 ? "Ada" : "Grace"));
    }
}