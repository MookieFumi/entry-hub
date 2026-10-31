namespace NewApi.Orders;

internal static class OrdersEndpoints
{
    public static void MapOrders(this IEndpointRouteBuilder app)
    {
        Order[] orders =
        [
            new(1, "Ada", 120.50m),
            new(2, "Grace", 80.00m),
        ];

        app.MapGet("/api/v1/orders", () => orders);
    }
}