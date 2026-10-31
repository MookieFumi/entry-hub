namespace LegacyApi.Orders;

internal static class OrdersEndpoints
{
    public static void MapOrders(this IEndpointRouteBuilder app)
    {
        LegacyOrder[] orders =
        [
            new(1, "Ada", 120.50m),
            new(2, "Grace", 80.00m),
        ];

        app.MapGet("/orders", () => orders);
    }
}