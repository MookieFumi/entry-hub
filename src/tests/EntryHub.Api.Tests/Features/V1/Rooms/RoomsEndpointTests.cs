using System.Net;
using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Text.Json;

using EntryHub.Api.Features.V1.Common;
using EntryHub.Api.Features.V1.Rooms;
using EntryHub.Api.Features.V1.Rooms.CreateRoom;
using EntryHub.Api.Security;
using EntryHub.Api.Tests.Support;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Tests.Features.V1.Rooms;

/// <summary>
/// Layer 3: the whole HTTP pipeline (routing, authorization, validation, serialization) over the real database.
/// </summary>
[Trait("Category", "Integration")]
public class RoomsEndpointTests
{
    private static string UniqueName() => $"Room {Guid.NewGuid():N}";

    [Fact]
    public async Task Anonymous_calls_get_401()
    {
        using var api = await TestApi.StartAsync();
        using var client = api.Anonymous();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/rooms")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/rooms/" + Guid.CreateVersion7())).StatusCode);
    }

    [Fact]
    public async Task An_employee_can_read_rooms_but_not_create_them()
    {
        using var api = await TestApi.StartAsync();
        using var employee = api.ClientFor("ana", Roles.Employee);

        Assert.Equal(HttpStatusCode.OK, (await employee.GetAsync("/api/v1/rooms")).StatusCode);

        var create = await employee.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(UniqueName(), 2, 6, null));
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    [Fact]
    public async Task A_token_without_a_known_role_gets_403()
    {
        using var api = await TestApi.StartAsync();
        using var client = api.ClientFor("visitor");

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/rooms")).StatusCode);
    }

    [Fact]
    public async Task Post_creates_the_room_and_get_returns_it()
    {
        using var api = await TestApi.StartAsync();
        using var client = api.ClientFor("fran", Roles.Facilities);
        var name = UniqueName();

        var created = await client.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(name, 2, 6, ["Projector"]));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var room = await created.Content.ReadFromJsonAsync<RoomResponse>();
        Assert.Equal($"/api/v1/rooms/{room!.Id}", created.Headers.Location?.ToString());

        var fetched = await client.GetFromJsonAsync<RoomResponse>(created.Headers.Location);
        Assert.Equal(name, fetched!.Name);
    }

    [Fact]
    public async Task Post_with_a_taken_name_returns_409_problem_details()
    {
        using var api = await TestApi.StartAsync();
        using var client = api.ClientFor("fran", Roles.Facilities);
        var request = new CreateRoomRequest(UniqueName(), 2, 6, null);
        await client.PostAsJsonAsync("/api/v1/rooms", request);

        var duplicate = await client.PostAsJsonAsync("/api/v1/rooms", request);

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var problem = await duplicate.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(409, problem!.Status);
    }

    [Theory]
    [InlineData("", 2, 6)]
    [InlineData("   ", 2, 6)]
    [InlineData("Valid", 2, 0)]
    [InlineData("Valid", 2, 501)]
    [InlineData("Valid", 101, 6)]
    public async Task Post_with_an_invalid_body_returns_400(string name, int floor, int capacity)
    {
        using var api = await TestApi.StartAsync();
        using var client = api.ClientFor("fran", Roles.Facilities);

        var response = await client.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(name, floor, capacity, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_unknown_room_returns_404()
    {
        using var api = await TestApi.StartAsync();
        using var client = api.ClientFor("ana", Roles.Employee);

        var response = await client.GetAsync($"/api/v1/rooms/{Guid.CreateVersion7()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_returns_a_page_with_the_total()
    {
        using var api = await TestApi.StartAsync();
        using var client = api.ClientFor("fran", Roles.Facilities);
        const int floor = 91;
        await using (var db = TestDatabase.CreateContext())
        {
            // Bookings point at rooms, so the ones of this floor go first.
            await db.Bookings.Where(booking => db.Rooms.Any(room => room.Id == booking.RoomId && room.Floor == floor)).ExecuteDeleteAsync();
            await db.Rooms.Where(room => room.Floor == floor).ExecuteDeleteAsync();
        }

        foreach (var capacity in new[] { 4, 8 })
        {
            await client.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(UniqueName(), floor, capacity, null));
        }

        var page = await client.GetFromJsonAsync<PagedResponse<RoomResponse>>($"/api/v1/rooms?floor={floor}&pageSize=1");

        Assert.Equal(2, page!.TotalCount);
        Assert.Single(page.Items);
    }

    [Fact]
    public async Task Occupancy_streams_server_sent_events()
    {
        using var api = await TestApi.StartAsync();
        using var client = api.ClientFor("fran", Roles.Facilities);
        var created = await client.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(UniqueName(), 1, 4, null));
        var room = await created.Content.ReadFromJsonAsync<RoomResponse>();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        using var response = await client.GetAsync(
            $"/api/v1/rooms/{room!.Id}/occupancy",
            HttpCompletionOption.ResponseHeadersRead,
            cancellation.Token);
        await using var body = await response.Content.ReadAsStreamAsync(cancellation.Token);
        var events = new List<SseItem<string>>();
        await foreach (var item in SseParser.Create(body).EnumerateAsync(cancellation.Token))
        {
            events.Add(item);
            if (events.Count == 2)
            {
                break;
            }
        }

        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        Assert.All(events, item => Assert.Equal("occupancy", item.EventType));
        using var json = JsonDocument.Parse(events[0].Data);
        Assert.Equal(room.Id, json.RootElement.GetProperty("roomId").GetGuid());
        Assert.Equal("Free", json.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Occupancy_of_unknown_room_returns_404()
    {
        using var api = await TestApi.StartAsync();
        using var client = api.ClientFor("ana", Roles.Employee);

        var response = await client.GetAsync($"/api/v1/rooms/{Guid.CreateVersion7()}/occupancy");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}