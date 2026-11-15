using System.Net;
using System.Net.Http.Json;

using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Features.V1.Bookings;
using EntryHub.Api.Features.V1.Bookings.CancelBooking;
using EntryHub.Api.Features.V1.Bookings.CreateBooking;
using EntryHub.Api.Features.V1.Rooms;
using EntryHub.Api.Features.V1.Rooms.CreateRoom;
using EntryHub.Api.Features.V1.Rules.UpdateRules;
using EntryHub.Api.Security;
using EntryHub.Api.Tests.Support;

namespace EntryHub.Api.Tests.Features.V1;

/// <summary>
/// Cancelling a recurring series from one occurrence on, with a cancellation notice of seven days.
/// </summary>
[Trait("Category", "Integration")]
public class CancelSeriesTests
{
    // Three weekly occurrences. The clock sits on Monday 2026-10-19, so the first one is inside the notice and the
    // other two are not.
    private static readonly DateTimeOffset Tuesday = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private sealed record Scenario(TestApi Api, Guid Tenant, string Employee, BookingResponse First);

    private static async Task<Scenario> StartAsync()
    {
        var tenant = await TestDatabase.CreateTenantAsync(Unique("tenant"));
        var api = await TestApi.StartAsync();
        using var facilities = api.ClientForTenant(tenant, Unique("fran"), Roles.Facilities);
        (await facilities.PutAsJsonAsync("/api/v1/rules", new UpdateRulesRequest(15, 240, 15, 15, 90, 10080, 5))).EnsureSuccessStatusCode();
        var room = (await (await facilities.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(Unique("Room"), 1, 8, null))).Content.ReadFromJsonAsync<RoomResponse>(TestJson.Options))!;

        var employee = Unique("ana");
        using var client = api.ClientForTenant(tenant, employee, Roles.Employee);
        var request = new CreateBookingRequest(room.Id, "Stand-up", Tuesday, Tuesday.AddHours(1), 4, Recurrence: new(RecurrenceFrequency.Weekly, 1, 3));
        var created = await client.PostAsJsonAsync("/api/v1/bookings", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        return new(api, tenant, employee, (await created.Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options))!);
    }

    private static async Task<BookingStatus> StatusAsync(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<BookingResponse>($"/api/v1/bookings/{id}", TestJson.Options))!.Status;

    [Fact]
    public async Task Cancelling_the_series_keeps_the_occurrences_inside_the_notice_and_reports_them()
    {
        var scenario = await StartAsync();
        using var api = scenario.Api;
        using var client = api.ClientForTenant(scenario.Tenant, scenario.Employee, Roles.Employee);
        var ids = scenario.First.Series!.Occurrences.Select(occurrence => occurrence.BookingId!.Value).ToList();

        var response = await client.PostAsync($"/api/v1/bookings/{ids[0]}/cancel?scope=series", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<CancelSeriesResponse>(TestJson.Options))!;
        Assert.Equal(ids.Skip(1), result.Cancelled);
        var kept = Assert.Single(result.Kept);
        Assert.Equal(ids[0], kept.BookingId);
        Assert.Equal(BookingRuleCodes.TooLateToCancel, kept.Violation.Code);
        Assert.Equal(BookingStatus.Confirmed, await StatusAsync(client, ids[0]));
        Assert.Equal(BookingStatus.Cancelled, await StatusAsync(client, ids[1]));
        Assert.Equal(BookingStatus.Cancelled, await StatusAsync(client, ids[2]));
    }

    [Fact]
    public async Task Cancelling_from_the_second_occurrence_leaves_the_earlier_ones_alone()
    {
        var scenario = await StartAsync();
        using var api = scenario.Api;
        using var client = api.ClientForTenant(scenario.Tenant, scenario.Employee, Roles.Employee);
        var ids = scenario.First.Series!.Occurrences.Select(occurrence => occurrence.BookingId!.Value).ToList();

        var result = (await (await client.PostAsync($"/api/v1/bookings/{ids[1]}/cancel?scope=series", null)).Content.ReadFromJsonAsync<CancelSeriesResponse>(TestJson.Options))!;

        Assert.Equal(ids.Skip(1), result.Cancelled);
        Assert.Empty(result.Kept);
        Assert.Equal(BookingStatus.Confirmed, await StatusAsync(client, ids[0]));
    }

    [Fact]
    public async Task The_default_scope_cancels_only_the_occurrence()
    {
        var scenario = await StartAsync();
        using var api = scenario.Api;
        using var client = api.ClientForTenant(scenario.Tenant, scenario.Employee, Roles.Employee);
        var ids = scenario.First.Series!.Occurrences.Select(occurrence => occurrence.BookingId!.Value).ToList();

        var response = await client.PostAsync($"/api/v1/bookings/{ids[1]}/cancel", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(BookingStatus.Cancelled, await StatusAsync(client, ids[1]));
        Assert.Equal(BookingStatus.Confirmed, await StatusAsync(client, ids[2]));
    }

    [Fact]
    public async Task Facilities_cancel_the_whole_series_inside_the_notice_and_others_cannot_touch_it()
    {
        var scenario = await StartAsync();
        using var api = scenario.Api;
        var id = scenario.First.Id;
        using var facilities = api.ClientForTenant(scenario.Tenant, Unique("fran"), Roles.Facilities);
        using var stranger = api.ClientForTenant(scenario.Tenant, Unique("eva"), Roles.Employee);
        using var otherTenant = api.ClientForTenant(Guid.NewGuid(), Unique("fran"), Roles.Facilities);

        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.PostAsync($"/api/v1/bookings/{id}/cancel?scope=series", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherTenant.PostAsync($"/api/v1/bookings/{id}/cancel?scope=series", null)).StatusCode);

        var result = (await (await facilities.PostAsync($"/api/v1/bookings/{id}/cancel?scope=series", null)).Content.ReadFromJsonAsync<CancelSeriesResponse>(TestJson.Options))!;

        Assert.Equal(3, result.Cancelled.Count);
        Assert.Empty(result.Kept);
    }

    [Fact]
    public async Task An_unknown_scope_returns_400()
    {
        var scenario = await StartAsync();
        using var api = scenario.Api;
        using var client = api.ClientForTenant(scenario.Tenant, scenario.Employee, Roles.Employee);

        var response = await client.PostAsync($"/api/v1/bookings/{scenario.First.Id}/cancel?scope=forever", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}