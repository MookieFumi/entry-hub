namespace NewApi.Orders;

internal sealed record Order(int Id, string Customer, decimal Total);