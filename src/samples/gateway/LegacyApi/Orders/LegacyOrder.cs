using System.Text.Json.Serialization;

namespace LegacyApi.Orders;

internal sealed record LegacyOrder(
    [property: JsonPropertyName("ORDER_ID")] int OrderId,
    [property: JsonPropertyName("CUST_NAME")] string CustomerName,
    [property: JsonPropertyName("TOTAL_AMT")] decimal TotalAmount);