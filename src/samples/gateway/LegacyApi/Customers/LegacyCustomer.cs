using System.Text.Json.Serialization;

namespace LegacyApi.Customers;

internal sealed record LegacyCustomer(
    [property: JsonPropertyName("CUST_ID")] int CustomerId,
    [property: JsonPropertyName("CUST_NAME")] string CustomerName);