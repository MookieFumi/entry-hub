using System.Net;
using System.Net.Http.Json;

using EntryHub.Api.Features.V1.Bookings;
using EntryHub.Api.Features.V1.Bookings.CreateBooking;
using EntryHub.Api.Features.V1.Bookings.RescheduleBooking;
using EntryHub.Api.Features.V1.Rooms;
using EntryHub.Api.Features.V1.Rooms.CreateRoom;
using EntryHub.Api.Features.V1.Sites;
using EntryHub.Api.Features.V1.Sites.CreateSite;
using EntryHub.Api.Features.V1.Sites.Holidays;
using EntryHub.Api.Security;
using EntryHub.Api.Tests.Support;

namespace EntryHub.Api.Tests.Features.V1;

/// <summary>
/// Holidays of a site: who can manage them and how they close the rooms of the site.
/// </summary>
[Trait("Category", "Integration")]
public class SiteHolidaysTests
{
    private static readonly DateTimeOffset Tuesday = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero); // 10:00 in Madrid
    private static readonly DateOnly TuesdayDate = new(2026, 10, 20);

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    /// <summary>
    /// A site and a room of their own, so a holiday does not close the rooms other tests use.
    /// </summary>
    private static async Task<(SiteResponse Site, RoomResponse Room)> NewSiteWithRoomAsync(HttpClient facilities)
    {
        var siteResponse = await facilities.PostAsJsonAsync("/api/v1/sites", new CreateSiteRequest(Unique("Site"), "Europe/Madrid", null, null, null));
        siteResponse.EnsureSuccessStatusCode();
        var site = (await siteResponse.Content.ReadFromJsonAsync<SiteResponse>(TestJson.Options))!;
        var roomResponse = await facilities.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(Unique("Room"), 1, 8, null, site.Id));
        roomResponse.EnsureSuccessStatusCode();
        return (site, (await roomResponse.Content.ReadFromJsonAsync<RoomResponse>(TestJson.Options))!);
    }

    private static CreateBookingRequest Booking(Guid roomId, DateTimeOffset? start = null) =>
        new(roomId, "Planning", start ?? Tuesday, (start ?? Tuesday).AddHours(1), 4);

    [Fact]
    public async Task Only_facilities_manage_holidays_and_everyone_can_list_them()
    {
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        using var employee = api.ClientFor(Unique("ana"), Roles.Employee);
        var (site, _) = await NewSiteWithRoomAsync(facilities);

        var forbidden = await employee.PostAsJsonAsync($"/api/v1/sites/{site.Id}/holidays", new CreateHolidayRequest(TuesdayDate, "Closed"));
        var created = await facilities.PostAsJsonAsync($"/api/v1/sites/{site.Id}/holidays", new CreateHolidayRequest(TuesdayDate, " Closed "));

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var holiday = (await created.Content.ReadFromJsonAsync<SiteHolidayResponse>(TestJson.Options))!;
        Assert.Equal("Closed", holiday.Name);

        var listed = await employee.GetFromJsonAsync<List<SiteHolidayResponse>>($"/api/v1/sites/{site.Id}/holidays", TestJson.Options);
        Assert.Equal([holiday.Id], listed!.Select(item => item.Id));

        Assert.Equal(HttpStatusCode.Forbidden, (await employee.DeleteAsync($"/api/v1/sites/{site.Id}/holidays/{holiday.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await facilities.DeleteAsync($"/api/v1/sites/{site.Id}/holidays/{holiday.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await facilities.DeleteAsync($"/api/v1/sites/{site.Id}/holidays/{holiday.Id}")).StatusCode);
    }

    [Fact]
    public async Task The_same_day_twice_is_a_conflict_and_an_unknown_site_is_404()
    {
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        var (site, _) = await NewSiteWithRoomAsync(facilities);
        (await facilities.PostAsJsonAsync($"/api/v1/sites/{site.Id}/holidays", new CreateHolidayRequest(TuesdayDate, null))).EnsureSuccessStatusCode();

        var again = await facilities.PostAsJsonAsync($"/api/v1/sites/{site.Id}/holidays", new CreateHolidayRequest(TuesdayDate, null));
        var unknown = await facilities.PostAsJsonAsync($"/api/v1/sites/{Guid.NewGuid()}/holidays", new CreateHolidayRequest(TuesdayDate, null));

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await facilities.GetAsync($"/api/v1/sites/{Guid.NewGuid()}/holidays")).StatusCode);
    }

    [Fact]
    public async Task The_list_can_be_limited_to_a_range_of_dates()
    {
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        var (site, _) = await NewSiteWithRoomAsync(facilities);

        foreach (var day in new[] { TuesdayDate, TuesdayDate.AddDays(7), TuesdayDate.AddDays(14) })
        {
            (await facilities.PostAsJsonAsync($"/api/v1/sites/{site.Id}/holidays", new CreateHolidayRequest(day, null))).EnsureSuccessStatusCode();
        }

        var middle = await facilities.GetFromJsonAsync<List<SiteHolidayResponse>>(
            $"/api/v1/sites/{site.Id}/holidays?from=2026-10-21&to=2026-10-30", TestJson.Options);

        Assert.Equal([TuesdayDate.AddDays(7)], middle!.Select(item => item.Date));
    }

    [Fact]
    public async Task A_booking_on_a_holiday_is_refused_and_works_again_once_the_holiday_is_removed()
    {
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        using var ana = api.ClientFor(Unique("ana"), Roles.Employee);
        var (site, room) = await NewSiteWithRoomAsync(facilities);
        var created = await facilities.PostAsJsonAsync($"/api/v1/sites/{site.Id}/holidays", new CreateHolidayRequest(TuesdayDate, "Fiesta"));
        var holiday = (await created.Content.ReadFromJsonAsync<SiteHolidayResponse>(TestJson.Options))!;

        var refused = await ana.PostAsJsonAsync("/api/v1/bookings", Booking(room.Id));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Contains("SiteClosed", await refused.Content.ReadAsStringAsync());
        Assert.Contains("Fiesta", await refused.Content.ReadAsStringAsync());

        (await facilities.DeleteAsync($"/api/v1/sites/{site.Id}/holidays/{holiday.Id}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Created, (await ana.PostAsJsonAsync("/api/v1/bookings", Booking(room.Id))).StatusCode);
    }

    [Fact]
    public async Task Rescheduling_onto_a_holiday_is_refused()
    {
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        using var ana = api.ClientFor(Unique("ana"), Roles.Employee);
        var (site, room) = await NewSiteWithRoomAsync(facilities);
        var booking = (await (await ana.PostAsJsonAsync("/api/v1/bookings", Booking(room.Id))).Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options))!;
        var wednesday = TuesdayDate.AddDays(1);
        (await facilities.PostAsJsonAsync($"/api/v1/sites/{site.Id}/holidays", new CreateHolidayRequest(wednesday, null))).EnsureSuccessStatusCode();

        var moved = await ana.PostAsJsonAsync(
            $"/api/v1/bookings/{booking.Id}/reschedule",
            new RescheduleBookingRequest(Tuesday.AddDays(1), Tuesday.AddDays(1).AddHours(1)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, moved.StatusCode);
        Assert.Contains("SiteClosed", await moved.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Other_sites_stay_open_and_the_search_skips_the_closed_one()
    {
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        using var ana = api.ClientFor(Unique("ana"), Roles.Employee);
        var (closedSite, closedRoom) = await NewSiteWithRoomAsync(facilities);
        var (_, openRoom) = await NewSiteWithRoomAsync(facilities);
        (await facilities.PostAsJsonAsync($"/api/v1/sites/{closedSite.Id}/holidays", new CreateHolidayRequest(TuesdayDate, null))).EnsureSuccessStatusCode();

        var from = Uri.EscapeDataString(Tuesday.ToString("O"));
        var to = Uri.EscapeDataString(Tuesday.AddHours(1).ToString("O"));
        var found = await ana.GetFromJsonAsync<List<RoomResponse>>($"/api/v1/rooms/availability?from={from}&to={to}", TestJson.Options);

        Assert.DoesNotContain(found!, room => room.Id == closedRoom.Id);
        Assert.Contains(found!, room => room.Id == openRoom.Id);
    }
}