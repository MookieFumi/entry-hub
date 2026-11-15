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
/// Facilities booking for another user with <c>onBehalfOf</c>.
/// </summary>
[Trait("Category", "Integration")]
public class OnBehalfOfTests
{
    private static readonly DateTimeOffset Tuesday = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static async Task<RoomResponse> NewRoomAsync(HttpClient facilities)
    {
        var response = await facilities.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(Unique("Room"), 1, 8, null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RoomResponse>(TestJson.Options))!;
    }

    [Fact]
    public async Task Facilities_book_for_someone_else_who_then_owns_the_booking()
    {
        using var api = await TestApi.StartAsync();
        var fran = Unique("fran");
        var ana = Unique("ana");
        using var facilities = api.ClientFor(fran, Roles.Facilities);
        using var employee = api.ClientFor(ana, Roles.Employee);
        var room = await NewRoomAsync(facilities);

        var created = await facilities.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(room.Id, "Board meeting", Tuesday, Tuesday.AddHours(1), 4, $" {ana} "));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var booking = (await created.Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options))!;
        Assert.Equal(ana, booking.OrganizerId);
        Assert.Equal(fran, booking.CreatedBy);

        var mine = await employee.GetFromJsonAsync<EntryHub.Api.Features.V1.Common.PagedResponse<BookingResponse>>("/api/v1/bookings/mine", TestJson.Options);
        Assert.Contains(mine!.Items, item => item.Id == booking.Id);
        Assert.Equal(HttpStatusCode.NoContent, (await employee.PostAsync($"/api/v1/bookings/{booking.Id}/cancel", null)).StatusCode);
    }

    [Fact]
    public async Task An_employee_cannot_book_on_behalf_of_someone_else()
    {
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        using var employee = api.ClientFor(Unique("ana"), Roles.Employee);
        var room = await NewRoomAsync(facilities);

        var response = await employee.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(room.Id, "Sneaky", Tuesday, Tuesday.AddHours(1), 4, Unique("bea")));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_booking_made_for_oneself_reports_the_organizer_as_creator()
    {
        using var api = await TestApi.StartAsync();
        var ana = Unique("ana");
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        using var employee = api.ClientFor(ana, Roles.Employee);
        var room = await NewRoomAsync(facilities);

        var created = await employee.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(room.Id, "Mine", Tuesday, Tuesday.AddHours(1), 4));

        var booking = (await created.Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options))!;
        Assert.Equal(ana, booking.OrganizerId);
        Assert.Equal(ana, booking.CreatedBy);
    }

    [Fact]
    public async Task Naming_oneself_is_allowed_for_facilities_and_a_blank_name_is_ignored()
    {
        using var api = await TestApi.StartAsync();
        var fran = Unique("fran");
        using var facilities = api.ClientFor(fran, Roles.Facilities);
        using var employee = api.ClientFor(Unique("ana"), Roles.Employee);
        var room = await NewRoomAsync(facilities);
        var other = await NewRoomAsync(facilities);

        var self = await facilities.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(room.Id, "Self", Tuesday, Tuesday.AddHours(1), 4, fran));
        var blank = await employee.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(other.Id, "Blank", Tuesday, Tuesday.AddHours(1), 4, "  "));

        Assert.Equal(fran, (await self.Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options))!.CreatedBy);
        Assert.Equal(HttpStatusCode.Created, blank.StatusCode);
    }
}