using System.Net;
using System.Net.Http.Json;

using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Features.V1.Bookings;
using EntryHub.Api.Features.V1.Bookings.CreateBooking;
using EntryHub.Api.Features.V1.Rooms;
using EntryHub.Api.Features.V1.Rooms.CreateRoom;
using EntryHub.Api.Security;
using EntryHub.Api.Tests.Support;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace EntryHub.Api.Tests.Features.V1;

/// <summary>
/// Broken object level authorization (OWASP API1:2023): one employee must not reach another employee's bookings
/// by putting their id in the URL.
/// </summary>
[Trait("Category", "Integration")]
public class ObjectLevelAuthorizationTests
{
    private static readonly DateTimeOffset Tuesday = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero); // 10:00 local

    /// <summary>
    /// Every endpoint that receives the id of something private to one person, and how to call it.
    /// </summary>
    private static readonly Dictionary<string, (HttpMethod Method, string Path)> PrivateResourceEndpoints = new()
    {
        ["GetBooking"] = (HttpMethod.Get, "/api/v1/bookings/{id}"),
        ["CancelBooking"] = (HttpMethod.Post, "/api/v1/bookings/{id}/cancel"),
    };

    /// <summary>
    /// Endpoints with an id whose resource every employee may see on purpose: rooms are shared.
    /// </summary>
    private static readonly HashSet<string> SharedResourceEndpoints = ["GetRoom", "StreamRoomOccupancy"];

    public static TheoryData<string> PrivateEndpointNames() => [.. PrivateResourceEndpoints.Keys];

    [Fact]
    public void Every_endpoint_with_an_id_is_classified()
    {
        // A new endpoint with an {id} makes this test fail until someone decides whether its resource is private
        // (and adds it to the attack below) or shared. Nothing gets in by default.
        using var api = new ApiFactory(TestDatabase.Unreachable);
        var endpoints = api.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.Parameters.Any(parameter => parameter.Name == "id"))
            .Select(endpoint => endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName ?? endpoint.RoutePattern.RawText!)
            .Order(StringComparer.Ordinal)
            .ToList();

        var classified = PrivateResourceEndpoints.Keys.Concat(SharedResourceEndpoints).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(classified, endpoints);
    }

    [Theory]
    [MemberData(nameof(PrivateEndpointNames))]
    public async Task An_employee_cannot_reach_a_booking_of_someone_else(string endpointName)
    {
        using var api = await TestApi.StartAsync();
        using var ana = api.ClientFor($"ana-{Guid.NewGuid():N}", Roles.Employee);
        using var bea = api.ClientFor($"bea-{Guid.NewGuid():N}", Roles.Employee);
        var booking = await NewBookingAsync(api, ana);
        var (method, path) = PrivateResourceEndpoints[endpointName];

        var response = await bea.SendAsync(new HttpRequestMessage(method, path.Replace("{id}", booking.Id.ToString(), StringComparison.Ordinal)));

        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Forbidden, HttpStatusCode.NotFound });

        var afterwards = await ana.GetFromJsonAsync<BookingResponse>($"/api/v1/bookings/{booking.Id}", TestJson.Options);
        Assert.Equal(BookingStatus.Confirmed, afterwards!.Status);
    }

    private static async Task<BookingResponse> NewBookingAsync(TestApi api, HttpClient organizer)
    {
        using var facilities = api.ClientFor($"facilities-{Guid.NewGuid():N}", Roles.Facilities);
        var roomResponse = await facilities.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest($"Room {Guid.NewGuid():N}", 1, 8, null));
        roomResponse.EnsureSuccessStatusCode();
        var room = (await roomResponse.Content.ReadFromJsonAsync<RoomResponse>())!;

        var bookingResponse = await organizer.PostAsJsonAsync(
            "/api/v1/bookings",
            new CreateBookingRequest(room.Id, "Planning", Tuesday, Tuesday.AddHours(1), 4));
        bookingResponse.EnsureSuccessStatusCode();

        return (await bookingResponse.Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options))!;
    }
}