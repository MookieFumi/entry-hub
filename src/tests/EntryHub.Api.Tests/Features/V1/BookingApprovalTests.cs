using System.Net;
using System.Net.Http.Json;

using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Features.V1.Bookings;
using EntryHub.Api.Features.V1.Bookings.CreateBooking;
using EntryHub.Api.Features.V1.Bookings.DecideBooking;
using EntryHub.Api.Features.V1.Common;
using EntryHub.Api.Features.V1.Rooms;
using EntryHub.Api.Features.V1.Rooms.CreateRoom;
using EntryHub.Api.Security;
using EntryHub.Api.Tests.Support;

namespace EntryHub.Api.Tests.Features.V1;

/// <summary>
/// Approving and rejecting pending bookings.
/// </summary>
[Trait("Category", "Integration")]
public class BookingApprovalTests
{
    private static readonly DateTimeOffset Tuesday = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static async Task<(Guid RoomId, BookingResponse Pending)> NewPendingAsync(HttpClient facilities, HttpClient employee)
    {
        var room = (await (await facilities.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(Unique("Room"), 1, 8, null, RequiresApproval: true))).Content.ReadFromJsonAsync<RoomResponse>(TestJson.Options))!;
        var created = await employee.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(room.Id, "Planning", Tuesday, Tuesday.AddHours(1), 4));
        return (room.Id, (await created.Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options))!);
    }

    [Fact]
    public async Task Only_facilities_decide()
    {
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        using var ana = api.ClientFor(Unique("ana"), Roles.Employee);
        var (_, pending) = await NewPendingAsync(facilities, ana);

        Assert.Equal(HttpStatusCode.Forbidden, (await ana.PostAsync($"/api/v1/bookings/{pending.Id}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ana.PostAsync($"/api/v1/bookings/{pending.Id}/reject", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await facilities.PostAsync($"/api/v1/bookings/{Guid.NewGuid()}/approve", null)).StatusCode);
    }

    [Fact]
    public async Task Approving_confirms_the_booking_and_says_who_decided()
    {
        using var api = await TestApi.StartAsync();
        var fran = Unique("fran");
        using var facilities = api.ClientFor(fran, Roles.Facilities);
        using var ana = api.ClientFor(Unique("ana"), Roles.Employee);
        var (_, pending) = await NewPendingAsync(facilities, ana);

        var approved = await facilities.PostAsync($"/api/v1/bookings/{pending.Id}/approve", null);

        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var booking = (await approved.Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options))!;
        Assert.Equal((BookingStatus.Confirmed, fran), (booking.Status, booking.DecidedBy));
        Assert.NotNull(booking.DecidedAt);
        Assert.Equal(HttpStatusCode.Conflict, (await facilities.PostAsync($"/api/v1/bookings/{pending.Id}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await facilities.PostAsync($"/api/v1/bookings/{pending.Id}/reject", null)).StatusCode);
    }

    [Fact]
    public async Task Rejecting_cancels_the_booking_with_the_reason_and_frees_the_room()
    {
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        using var ana = api.ClientFor(Unique("ana"), Roles.Employee);
        using var luis = api.ClientFor(Unique("luis"), Roles.Employee);
        var (roomId, pending) = await NewPendingAsync(facilities, ana);

        var rejected = await facilities.PostAsJsonAsync($"/api/v1/bookings/{pending.Id}/reject", new RejectBookingRequest(" Needed for the board "));

        var booking = (await rejected.Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options))!;
        Assert.Equal((BookingStatus.Cancelled, "Needed for the board"), (booking.Status, booking.DecisionReason));
        Assert.NotNull(booking.CancelledAt);
        Assert.Equal(HttpStatusCode.Created, (await luis.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(roomId, "Retro", Tuesday, Tuesday.AddHours(1), 4))).StatusCode);
    }

    [Fact]
    public async Task A_rejection_needs_no_body_and_facilities_list_what_is_pending()
    {
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        using var ana = api.ClientFor(Unique("ana"), Roles.Employee);
        var (roomId, pending) = await NewPendingAsync(facilities, ana);

        var listed = await facilities.GetFromJsonAsync<PagedResponse<BookingResponse>>($"/api/v1/bookings?status=Pending&roomId={roomId}", TestJson.Options);
        var rejected = await facilities.PostAsync($"/api/v1/bookings/{pending.Id}/reject", null);

        Assert.Equal([pending.Id], listed!.Items.Select(item => item.Id));
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Null((await rejected.Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options))!.DecisionReason);
    }
}