using System.Net;
using System.Net.Http.Json;

using EntryHub.Api.Features.V1.Bookings;
using EntryHub.Api.Features.V1.Bookings.CreateBooking;
using EntryHub.Api.Features.V1.Rooms;
using EntryHub.Api.Features.V1.Rooms.CreateRoom;
using EntryHub.Api.Security;
using EntryHub.Api.Tests.Support;

namespace EntryHub.Api.Tests.Features.V1.Bookings;

/// <summary>
/// The <c>Idempotency-Key</c> header of <c>POST /bookings</c>.
/// </summary>
[Trait("Category", "Integration")]
public class IdempotencyKeyTests
{
    private static readonly DateTimeOffset Tuesday = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static async Task<RoomResponse> NewRoomAsync(TestApi api)
    {
        using var facilities = api.ClientFor(Unique("facilities"), Roles.Facilities);
        var response = await facilities.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(Unique("Room"), 1, 8, null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RoomResponse>())!;
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, CreateBookingRequest body, string? key)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/bookings") { Content = JsonContent.Create(body) };

        if (key is not null)
        {
            message.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        }

        return await client.SendAsync(message);
    }

    private static CreateBookingRequest Request(Guid roomId, string title = "Planning") =>
        new(roomId, title, Tuesday, Tuesday.AddHours(1), 4);

    [Fact]
    public async Task Repeating_a_request_with_the_same_key_returns_the_original_booking()
    {
        using var api = await TestApi.StartAsync();
        var room = await NewRoomAsync(api);
        using var client = api.ClientFor(Unique("ana"), Roles.Employee);
        var key = Unique("key");

        var first = await PostAsync(client, Request(room.Id), key);
        var second = await PostAsync(client, Request(room.Id), key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.False(first.Headers.Contains("Idempotent-Replayed"));
        Assert.Equal("true", second.Headers.GetValues("Idempotent-Replayed").Single());
        var original = await first.Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options);
        var replay = await second.Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options);
        Assert.Equal(original!.Id, replay!.Id);

        var mine = await client.GetFromJsonAsync<EntryHub.Api.Features.V1.Common.PagedResponse<BookingResponse>>("/api/v1/bookings/mine", TestJson.Options);
        Assert.Single(mine!.Items);
    }

    [Fact]
    public async Task Reusing_a_key_with_a_different_body_returns_422()
    {
        using var api = await TestApi.StartAsync();
        var room = await NewRoomAsync(api);
        using var client = api.ClientFor(Unique("ana"), Roles.Employee);
        var key = Unique("key");
        (await PostAsync(client, Request(room.Id), key)).EnsureSuccessStatusCode();

        var different = await PostAsync(client, Request(room.Id, title: "Another meeting"), key);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, different.StatusCode);
    }

    [Fact]
    public async Task The_same_key_of_two_users_creates_two_bookings()
    {
        using var api = await TestApi.StartAsync();
        var first = await NewRoomAsync(api);
        var second = await NewRoomAsync(api);
        using var ana = api.ClientFor(Unique("ana"), Roles.Employee);
        using var bea = api.ClientFor(Unique("bea"), Roles.Employee);
        var key = Unique("key");

        var anaBooking = await PostAsync(ana, Request(first.Id), key);
        var beaBooking = await PostAsync(bea, Request(second.Id), key);

        Assert.Equal(HttpStatusCode.Created, anaBooking.StatusCode);
        Assert.Equal(HttpStatusCode.Created, beaBooking.StatusCode);
        Assert.False(beaBooking.Headers.Contains("Idempotent-Replayed"));
    }

    [Fact]
    public async Task Requests_without_a_key_are_not_deduplicated()
    {
        using var api = await TestApi.StartAsync();
        var first = await NewRoomAsync(api);
        var second = await NewRoomAsync(api);
        using var client = api.ClientFor(Unique("ana"), Roles.Employee);

        Assert.Equal(HttpStatusCode.Created, (await PostAsync(client, Request(first.Id), null)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await PostAsync(client, Request(second.Id), null)).StatusCode);
    }

    [Fact]
    public async Task An_empty_or_too_long_key_returns_400()
    {
        using var api = await TestApi.StartAsync();
        var room = await NewRoomAsync(api);
        using var client = api.ClientFor(Unique("ana"), Roles.Employee);

        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(client, Request(room.Id), new string('k', 101))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(client, Request(room.Id), " ")).StatusCode);
    }

    [Fact]
    public async Task Simultaneous_requests_with_one_key_create_one_booking()
    {
        using var api = await TestApi.StartAsync();
        var room = await NewRoomAsync(api);
        using var client = api.ClientFor(Unique("ana"), Roles.Employee);
        var key = Unique("key");

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => PostAsync(client, Request(room.Id), key)));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        var ids = new HashSet<Guid>();

        foreach (var response in responses)
        {
            ids.Add((await response.Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options))!.Id);
        }

        Assert.Single(ids);
    }
}