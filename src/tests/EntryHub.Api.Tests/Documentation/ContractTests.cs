using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

using EntryHub.Api.Features.V1.Rooms;
using EntryHub.Api.Features.V1.Rooms.CreateRoom;
using EntryHub.Api.Security;
using EntryHub.Api.Tests.Support;

using Json.Schema;

namespace EntryHub.Api.Tests.Documentation;

/// <summary>
/// The contract in <c>contracts/EntryHub.Api.json</c> is generated from the code, so CI already fails when it
/// changes without being committed. This test checks the other direction: that what the running API answers
/// matches the response schema the contract declares for that operation.
/// </summary>
[Trait("Category", "Integration")]
public class ContractTests
{
    private static readonly string ContractPath = Path.Combine(AppContext.BaseDirectory, "contracts", "EntryHub.Api.json");

    [Fact]
    public async Task Responses_of_the_api_match_the_schemas_of_the_contract()
    {
        var contract = JsonNode.Parse(await File.ReadAllTextAsync(ContractPath, CancellationToken.None))!;
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientFor("fran", Roles.Facilities);

        var created = await facilities.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest($"Room {Guid.NewGuid():N}", 2, 6, ["Projector"]), CancellationToken.None);
        var room = await created.Content.ReadFromJsonAsync<RoomResponse>(CancellationToken.None);

        // Each call: the request, the contract entry it answers and the status the contract promises for it.
        var calls = new[]
        {
            (Response: created, Path: "/api/v1/rooms", Method: "post", Status: "201"),
            (Response: await facilities.GetAsync($"/api/v1/rooms/{room!.Id}", CancellationToken.None), Path: "/api/v1/rooms/{id}", Method: "get", Status: "200"),
            (Response: await facilities.GetAsync("/api/v1/rooms", CancellationToken.None), Path: "/api/v1/rooms", Method: "get", Status: "200"),
            (Response: await facilities.GetAsync("/api/v1/health", CancellationToken.None), Path: "/api/v1/health", Method: "get", Status: "200"),
        };

        foreach (var (response, path, method, status) in calls)
        {
            Assert.Equal(status, ((int)response.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture));

            var schema = SchemaOf(contract, path, method, status, response.Content.Headers.ContentType?.MediaType);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
            var result = schema.Evaluate(body.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });

            Assert.True(result.IsValid, $"{method.ToUpperInvariant()} {path} -> {status} does not match the contract: {JsonSerializer.Serialize(result)}");
        }
    }

    /// <summary>
    /// The response schema of one operation, with the schemas of the contract moved to <c>$defs</c> and the
    /// <c>#/components/schemas/...</c> references pointed there, because JSON Schema has no <c>components</c> keyword.
    /// </summary>
    private static JsonSchema SchemaOf(JsonNode contract, string path, string method, string status, string? mediaType)
    {
        var content = contract["paths"]![path]![method]!["responses"]![status]!["content"]!;
        var schema = content[mediaType ?? "application/json"]!["schema"]!.DeepClone();
        schema["$defs"] = contract["components"]!["schemas"]!.DeepClone();
        return JsonSchema.FromText(schema.ToJsonString().Replace("#/components/schemas/", "#/$defs/", StringComparison.Ordinal));
    }
}