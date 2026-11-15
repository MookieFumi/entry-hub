using System.Net;
using System.Net.Http.Json;

using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Features.V1.Bookings.CreateBooking;
using EntryHub.Api.Features.V1.Common;
using EntryHub.Api.Features.V1.Rooms;
using EntryHub.Api.Features.V1.Rooms.CreateRoom;
using EntryHub.Api.Features.V1.Rules.GetRules;
using EntryHub.Api.Features.V1.Rules.UpdateRules;
using EntryHub.Api.Features.V1.Sites;
using EntryHub.Api.Features.V1.Sites.CreateSite;
using EntryHub.Api.Features.V1.Sites.UpdateSite;
using EntryHub.Api.Security;
using EntryHub.Api.Tests.Support;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Tests.Features.V1;

/// <summary>
/// Sites and the rules of each tenant. Every test works in a tenant of its own, so changing the rules of one never
/// reaches another test.
/// </summary>
[Trait("Category", "Integration")]
public class SitesAndRulesTests
{
    // Tuesday 2026-10-20, 10:00 in Madrid and 09:00 in London.
    private static readonly DateTimeOffset Tuesday = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static async Task<Guid> NewTenantAsync()
    {
        await TestDatabase.EnsureMigratedAsync();
        var id = Guid.NewGuid();
        await using var db = TestDatabase.CreateContext();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO tenants (\"Id\", \"Name\", \"CreatedAt\") VALUES ({id}, {Unique("Tenant")}, now())");
        return id;
    }

    private static async Task<SiteResponse> NewSiteAsync(
        HttpClient facilities,
        string? name = null,
        string zone = "Europe/Madrid",
        string? opens = null,
        string? closes = null,
        List<DayOfWeek>? days = null)
    {
        var response = await facilities.PostAsJsonAsync("/api/v1/sites", new CreateSiteRequest(name ?? Unique("Site"), zone, opens, closes, days));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SiteResponse>(TestJson.Options))!;
    }

    private static async Task<RoomResponse> NewRoomAsync(HttpClient facilities, Guid? siteId = null, string? name = null)
    {
        var response = await facilities.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(name ?? Unique("Room"), 1, 8, null, siteId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RoomResponse>(TestJson.Options))!;
    }

    private static Task<CreateBookingResult> BookAsync(Guid tenantId, Guid roomId, DateTimeOffset start, TimeSpan length)
    {
        return Run();

        async Task<CreateBookingResult> Run()
        {
            await using var db = TestDatabase.CreateContext(tenantId: tenantId);
            var handler = new CreateBookingHandler(db, TestPolicy.Default(), TestPolicy.Reader(db), TestClock.Default);

            return await handler.HandleAsync(
                new Caller(Unique("ana"), IsFacilities: false),
                new CreateBookingRequest(roomId, "Planning", start, start + length, 4),
                CancellationToken.None);
        }
    }

    [Fact]
    public async Task Employees_read_sites_but_only_facilities_write_them()
    {
        var tenant = await NewTenantAsync();
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientForTenant(tenant, Unique("fran"), Roles.Facilities);
        using var employee = api.ClientForTenant(tenant, Unique("ana"), Roles.Employee);
        var site = await NewSiteAsync(facilities);

        var listed = await employee.GetFromJsonAsync<List<SiteResponse>>("/api/v1/sites", TestJson.Options);

        Assert.Equal([site.Id], listed!.Select(item => item.Id));
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PostAsJsonAsync("/api/v1/sites", new CreateSiteRequest("X", "Europe/Madrid", null, null, null))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PatchAsJsonAsync($"/api/v1/sites/{site.Id}", new UpdateSiteRequest("Y", null, null, null, null, null))).StatusCode);
    }

    [Fact]
    public async Task A_new_site_has_office_hours_by_default_and_a_repeated_name_is_a_conflict()
    {
        var tenant = await NewTenantAsync();
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientForTenant(tenant, Unique("fran"), Roles.Facilities);
        var site = await NewSiteAsync(facilities);

        Assert.Equal(("08:00", "20:00"), (site.Opens, site.Closes));
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday], site.OpenDays);

        var again = await facilities.PostAsJsonAsync("/api/v1/sites", new CreateSiteRequest(site.Name, "Europe/Madrid", null, null, null));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Theory]
    [InlineData("Mars/Base", "08:00", "20:00")]
    [InlineData("Europe/Madrid", "8am", "20:00")]
    [InlineData("Europe/Madrid", "20:00", "08:00")]
    public async Task A_site_with_a_bad_zone_or_hours_is_rejected(string zone, string opens, string closes)
    {
        var tenant = await NewTenantAsync();
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientForTenant(tenant, Unique("fran"), Roles.Facilities);

        var response = await facilities.PostAsJsonAsync("/api/v1/sites", new CreateSiteRequest(Unique("Site"), zone, opens, closes, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotEmpty((await response.Content.ReadFromJsonAsync<ValidationProblemDetails>())!.Errors);
    }

    [Fact]
    public async Task A_site_can_be_edited_and_a_deactivated_one_leaves_the_listings_with_its_rooms()
    {
        var tenant = await NewTenantAsync();
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientForTenant(tenant, Unique("fran"), Roles.Facilities);
        using var employee = api.ClientForTenant(tenant, Unique("ana"), Roles.Employee);
        var site = await NewSiteAsync(facilities);
        var other = await NewSiteAsync(facilities);
        var room = await NewRoomAsync(facilities, site.Id);

        var edited = await facilities.PatchAsJsonAsync($"/api/v1/sites/{site.Id}", new UpdateSiteRequest(null, "Europe/London", "09:00", "17:30", [DayOfWeek.Monday], null));
        var body = await edited.Content.ReadFromJsonAsync<SiteResponse>(TestJson.Options);
        Assert.Equal(("Europe/London", "09:00", "17:30"), (body!.TimeZone, body.Opens, body.Closes));
        Assert.Equal([DayOfWeek.Monday], body.OpenDays);

        await facilities.PatchAsJsonAsync($"/api/v1/sites/{site.Id}", new UpdateSiteRequest(null, null, null, null, null, false));

        var sites = await employee.GetFromJsonAsync<List<SiteResponse>>("/api/v1/sites", TestJson.Options);
        var all = await facilities.GetFromJsonAsync<List<SiteResponse>>("/api/v1/sites?includeInactive=true", TestJson.Options);
        var rooms = await employee.GetFromJsonAsync<PagedResponse<RoomResponse>>("/api/v1/rooms", TestJson.Options);

        Assert.Equal([other.Id], sites!.Select(item => item.Id));
        Assert.Equal(2, all!.Count);
        Assert.DoesNotContain(rooms!.Items, item => item.Id == room.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await facilities.PatchAsJsonAsync($"/api/v1/sites/{Guid.CreateVersion7()}", new UpdateSiteRequest("Z", null, null, null, null, null))).StatusCode);
    }

    [Fact]
    public async Task The_same_room_name_is_allowed_in_two_sites_and_a_conflict_in_one()
    {
        var tenant = await NewTenantAsync();
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientForTenant(tenant, Unique("fran"), Roles.Facilities);
        var first = await NewSiteAsync(facilities);
        var second = await NewSiteAsync(facilities);
        var name = Unique("Boardroom");

        await NewRoomAsync(facilities, first.Id, name);
        await NewRoomAsync(facilities, second.Id, name);
        var duplicate = await facilities.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(name, 1, 8, null, first.Id));

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task A_room_without_a_site_goes_to_the_oldest_active_site_and_an_unknown_site_is_a_400()
    {
        var tenant = await NewTenantAsync();
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientForTenant(tenant, Unique("fran"), Roles.Facilities);

        var noSite = await facilities.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(Unique("Room"), 1, 4, null));
        Assert.Equal(HttpStatusCode.BadRequest, noSite.StatusCode);

        var oldest = await NewSiteAsync(facilities);
        await NewSiteAsync(facilities);
        var room = await NewRoomAsync(facilities);
        var unknown = await facilities.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(Unique("Room"), 1, 4, null, Guid.CreateVersion7()));

        Assert.Equal(oldest.Id, room.SiteId);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
    }

    [Fact]
    public async Task Rooms_and_availability_can_be_filtered_by_site_and_a_site_of_another_tenant_is_not_found()
    {
        var tenant = await NewTenantAsync();
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientForTenant(tenant, Unique("fran"), Roles.Facilities);
        using var foreigner = api.ClientForTenant(await NewTenantAsync(), Unique("eve"), Roles.Facilities);
        var madrid = await NewSiteAsync(facilities);
        var london = await NewSiteAsync(facilities, zone: "Europe/London");
        var inMadrid = await NewRoomAsync(facilities, madrid.Id);
        var inLondon = await NewRoomAsync(facilities, london.Id);

        var rooms = await facilities.GetFromJsonAsync<PagedResponse<RoomResponse>>($"/api/v1/rooms?siteId={london.Id}", TestJson.Options);
        var free = await facilities.GetFromJsonAsync<List<RoomResponse>>(
            $"/api/v1/rooms/availability?from={Uri.EscapeDataString(Tuesday.ToString("O"))}&to={Uri.EscapeDataString(Tuesday.AddHours(1).ToString("O"))}&siteId={madrid.Id}",
            TestJson.Options);
        var foreign = await foreigner.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(Unique("Room"), 1, 4, null, madrid.Id));

        Assert.Equal([inLondon.Id], rooms!.Items.Select(room => room.Id));
        Assert.Equal([inMadrid.Id], free!.Select(room => room.Id));
        Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
    }

    [Fact]
    public async Task The_hours_and_the_zone_of_the_site_of_the_room_decide_the_booking()
    {
        var tenant = await NewTenantAsync();
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientForTenant(tenant, Unique("fran"), Roles.Facilities);
        // 08:00 UTC on Tuesday is 10:00 in Madrid and 09:00 in London.
        var madrid = await NewSiteAsync(facilities, opens: "10:00", closes: "12:00");
        var london = await NewSiteAsync(facilities, zone: "Europe/London", opens: "09:00", closes: "10:00");
        var closedTuesday = await NewSiteAsync(facilities, days: [DayOfWeek.Wednesday]);
        var inMadrid = await NewRoomAsync(facilities, madrid.Id);
        var inLondon = await NewRoomAsync(facilities, london.Id);
        var onWednesdays = await NewRoomAsync(facilities, closedTuesday.Id);

        var madridResult = await BookAsync(tenant, inMadrid.Id, Tuesday, TimeSpan.FromHours(2));
        var londonResult = await BookAsync(tenant, inLondon.Id, Tuesday, TimeSpan.FromHours(1));
        var tooLongForLondon = await BookAsync(tenant, inLondon.Id, Tuesday.AddHours(1), TimeSpan.FromHours(1));
        var wrongDay = await BookAsync(tenant, onWednesdays.Id, Tuesday, TimeSpan.FromHours(1));

        Assert.IsType<CreateBookingResult.Created>(madridResult);
        Assert.IsType<CreateBookingResult.Created>(londonResult);
        Assert.Contains(BookingRuleCodes.OutsideOfficeHours, Codes(tooLongForLondon));
        Assert.Contains(BookingRuleCodes.OutsideOfficeHours, Codes(wrongDay));
    }

    private static string[] Codes(CreateBookingResult result) =>
        [.. Assert.IsType<CreateBookingResult.RulesBroken>(result).Violations.Select(violation => violation.Code)];

    [Fact]
    public async Task Without_a_stored_row_a_tenant_gets_the_configured_rules_and_each_tenant_sees_its_own()
    {
        var first = await NewTenantAsync();
        var second = await NewTenantAsync();
        using var api = await TestApi.StartAsync();
        using var firstFacilities = api.ClientForTenant(first, Unique("fran"), Roles.Facilities);
        using var secondEmployee = api.ClientForTenant(second, Unique("ana"), Roles.Employee);

        var defaults = await firstFacilities.GetFromJsonAsync<RulesResponse>("/api/v1/rules", TestJson.Options);
        Assert.Equal((15, 240, 5), (defaults!.MinDurationMinutes, defaults.MaxDurationMinutes, 5));

        var changed = await firstFacilities.PutAsJsonAsync("/api/v1/rules", new UpdateRulesRequest(30, 90, 30, 60, 30, 120, 2, 20));
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);

        var mine = await firstFacilities.GetFromJsonAsync<RulesResponse>("/api/v1/rules", TestJson.Options);
        var theirs = await secondEmployee.GetFromJsonAsync<RulesResponse>("/api/v1/rules", TestJson.Options);

        Assert.Equal((30, 90, 30, 60, 30, 120), (mine!.MinDurationMinutes, mine.MaxDurationMinutes, mine.SlotMinutes, mine.MinLeadTimeMinutes, mine.MaxAdvanceDays, mine.CancellationNoticeMinutes));
        Assert.Equal((0, 20), (defaults.AutoReleaseMinutes, mine.AutoReleaseMinutes));
        Assert.Equal(240, theirs!.MaxDurationMinutes);
        Assert.Equal(2, (await api.ClientForTenant(first, Unique("ana"), Roles.Employee).GetFromJsonAsync<RulesResponse>("/api/v1/rules", TestJson.Options))!.MaxActiveBookings);
    }

    [Fact]
    public async Task Only_facilities_edit_the_rules_and_absurd_values_are_rejected()
    {
        var tenant = await NewTenantAsync();
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientForTenant(tenant, Unique("fran"), Roles.Facilities);
        using var employee = api.ClientForTenant(tenant, Unique("ana"), Roles.Employee);
        var valid = new UpdateRulesRequest(15, 240, 15, 15, 90, 15, 5);

        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PutAsJsonAsync("/api/v1/rules", valid)).StatusCode);

        foreach (var absurd in new[]
        {
            valid with { MinDurationMinutes = 0 },
            valid with { MaxDurationMinutes = 5000 },
            valid with { MaxDurationMinutes = 10 },
            valid with { SlotMinutes = 7 },
            valid with { MinDurationMinutes = 20 },
            valid with { MaxAdvanceDays = 0 },
            valid with { MaxActiveBookings = -1 },
        })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await facilities.PutAsJsonAsync("/api/v1/rules", absurd)).StatusCode);
        }
    }

    [Fact]
    public async Task Two_tenants_with_different_rules_get_different_decisions_for_the_same_request()
    {
        var strict = await NewTenantAsync();
        var relaxed = await NewTenantAsync();
        using var api = await TestApi.StartAsync();
        using var strictFacilities = api.ClientForTenant(strict, Unique("fran"), Roles.Facilities);
        using var relaxedFacilities = api.ClientForTenant(relaxed, Unique("lola"), Roles.Facilities);
        var strictRoom = await NewRoomAsync(strictFacilities, (await NewSiteAsync(strictFacilities)).Id);
        var relaxedRoom = await NewRoomAsync(relaxedFacilities, (await NewSiteAsync(relaxedFacilities)).Id);
        Assert.Equal(HttpStatusCode.OK, (await strictFacilities.PutAsJsonAsync("/api/v1/rules", new UpdateRulesRequest(15, 60, 15, 15, 90, 15, 5))).StatusCode);

        var strictResult = await BookAsync(strict, strictRoom.Id, Tuesday, TimeSpan.FromHours(2));
        var relaxedResult = await BookAsync(relaxed, relaxedRoom.Id, Tuesday, TimeSpan.FromHours(2));

        Assert.Contains(BookingRuleCodes.DurationTooLong, Codes(strictResult));
        Assert.IsType<CreateBookingResult.Created>(relaxedResult);
    }

    [Fact]
    public async Task The_migration_gave_the_default_tenant_a_site()
    {
        await TestDatabase.EnsureMigratedAsync();
        await using var db = TestDatabase.CreateContext(tenantId: EntryHub.Api.Domain.Tenants.Tenant.DefaultId);

        Assert.True(await db.Sites.AnyAsync());
    }
}