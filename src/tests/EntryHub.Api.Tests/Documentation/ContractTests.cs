using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

using EntryHub.Api.Features.V1.Bookings.CreateBooking;
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
            await AssertMatchesAsync(contract, response, path, method, status);
        }
    }

    [Fact]
    public async Task Error_responses_match_the_contract()
    {
        var contract = JsonNode.Parse(await File.ReadAllTextAsync(ContractPath, CancellationToken.None))!;
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientFor("fran", Roles.Facilities);
        using var employee = api.ClientFor($"ana-{Guid.NewGuid():N}", Roles.Employee);
        using var other = api.ClientFor($"bea-{Guid.NewGuid():N}", Roles.Employee);

        var name = $"Room {Guid.NewGuid():N}";
        var created = await facilities.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(name, 2, 4, null), CancellationToken.None);
        var room = (await created.Content.ReadFromJsonAsync<RoomResponse>(CancellationToken.None))!;
        var tuesday = new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);
        var saturday = new DateTimeOffset(2026, 10, 24, 8, 0, 0, TimeSpan.Zero);
        (await employee.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(room.Id, "Planning", tuesday, tuesday.AddHours(1), 4), CancellationToken.None))
            .EnsureSuccessStatusCode();

        var calls = new[]
        {
            (Response: await facilities.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(name, 2, 4, null), CancellationToken.None), Path: "/api/v1/rooms", Method: "post", Status: "409"),
            (Response: await facilities.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest("", 2, 0, null), CancellationToken.None), Path: "/api/v1/rooms", Method: "post", Status: "400"),
            (Response: await facilities.GetAsync($"/api/v1/rooms/{Guid.CreateVersion7()}", CancellationToken.None), Path: "/api/v1/rooms/{id}", Method: "get", Status: "404"),
            (Response: await other.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(room.Id, "Planning", saturday, saturday.AddHours(1), 9), CancellationToken.None), Path: "/api/v1/bookings", Method: "post", Status: "422"),
            (Response: await other.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(room.Id, "", tuesday, tuesday.AddHours(1), 0), CancellationToken.None), Path: "/api/v1/bookings", Method: "post", Status: "400"),
            (Response: await other.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(room.Id, "Planning", tuesday.AddMinutes(30), tuesday.AddMinutes(90), 4), CancellationToken.None), Path: "/api/v1/bookings", Method: "post", Status: "409"),
        };

        foreach (var (response, path, method, status) in calls)
        {
            await AssertMatchesAsync(contract, response, path, method, status);
        }
    }

    private static async Task AssertMatchesAsync(JsonNode contract, HttpResponseMessage response, string path, string method, string status)
    {
        Assert.Equal(status, ((int)response.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture));

        var declared = contract["paths"]![path]![method]!["responses"]![status];
        Assert.True(declared is not null, $"{method.ToUpperInvariant()} {path} answers {status}, which the contract does not declare.");

        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);
        if (declared["content"] is null)
        {
            Assert.Empty(body);
            return;
        }

        var schema = SchemaOf(contract, path, method, status, response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(body);
        var result = schema.Evaluate(json.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });

        Assert.True(result.IsValid, $"{method.ToUpperInvariant()} {path} -> {status} does not match the contract: {JsonSerializer.Serialize(result)}");
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