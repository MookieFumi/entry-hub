using System.Net;
using System.Net.Http.Json;

using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Features.V1.Bookings;
using EntryHub.Api.Features.V1.Bookings.CreateBooking;
using EntryHub.Api.Features.V1.Rooms;
using EntryHub.Api.Features.V1.Rooms.CreateRoom;
using EntryHub.Api.Security;
using EntryHub.Api.Tests.Support;

namespace EntryHub.Api.Tests.Features.V1;

/// <summary>
/// Recurring bookings: the series, the skipped occurrences and the time zone of the site.
/// </summary>
[Trait("Category", "Integration")]
public class RecurringBookingsTests
{
    // 10:00 in the office on the last Tuesday of summer time (Europe/Madrid changes on 2026-10-25).
    private static readonly DateTimeOffset Tuesday = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static async Task<RoomResponse> NewRoomAsync(HttpClient facilities)
    {
        var response = await facilities.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(Unique("Room"), 1, 8, null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RoomResponse>(TestJson.Options))!;
    }

    private static CreateBookingRequest Weekly(Guid roomId, int count, DateTimeOffset? start = null) =>
        new(roomId, "Stand-up", start ?? Tuesday, (start ?? Tuesday).AddHours(1), 4, Recurrence: new RecurrenceRequest(RecurrenceFrequency.Weekly, 1, count));

    private static async Task<BookingResponse> CreatedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options))!;
    }

    [Fact]
    public async Task A_weekly_series_keeps_the_time_of_day_across_the_daylight_saving_change()
    {
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        var room = await NewRoomAsync(facilities);

        var first = await CreatedAsync(await facilities.PostAsJsonAsync("/api/v1/bookings", Weekly(room.Id, 3)));

        var series = first.Series!;
        Assert.Equal(first.SeriesId, series.Id);
        Assert.Equal(3, series.Requested);
        Assert.Equal(3, series.Created);
        Assert.Equal(
            [Tuesday, Tuesday.AddDays(7).AddHours(1), Tuesday.AddDays(14).AddHours(1)],
            series.Occurrences.Select(occurrence => occurrence.StartAt));
        Assert.All(series.Occurrences, occurrence => Assert.NotNull(occurrence.BookingId));

        var listed = await facilities.GetFromJsonAsync<EntryHub.Api.Features.V1.Common.PagedResponse<BookingResponse>>("/api/v1/bookings/mine", TestJson.Options);
        Assert.Equal([1, 2, 3], listed!.Items.OrderBy(booking => booking.SeriesPosition).Select(booking => booking.SeriesPosition!.Value));
        Assert.All(listed.Items, booking => Assert.Equal(first.SeriesId, booking.SeriesId));
    }

    [Fact]
    public async Task An_occurrence_that_breaks_a_rule_is_skipped_and_the_rest_are_booked()
    {
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        var room = await NewRoomAsync(facilities);
        var second = Tuesday.AddDays(7).AddHours(1);
        (await CreatedAsync(await facilities.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(room.Id, "Other", second, second.AddHours(1), 2)))).ToString();

        var first = await CreatedAsync(await facilities.PostAsJsonAsync("/api/v1/bookings", Weekly(room.Id, 3)));

        var series = first.Series!;
        Assert.Equal(2, series.Created);
        var skipped = Assert.Single(series.Occurrences, occurrence => occurrence.BookingId is null);
        Assert.Equal(2, skipped.Position);
        Assert.Equal(BookingRuleCodes.RoomAlreadyBooked, Assert.Single(skipped.Violations).Code);
    }

    [Fact]
    public async Task A_series_where_nothing_fits_returns_422()
    {
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        var room = await NewRoomAsync(facilities);
        (await facilities.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(room.Id, "Other", Tuesday, Tuesday.AddHours(1), 2))).EnsureSuccessStatusCode();

        var response = await facilities.PostAsJsonAsync("/api/v1/bookings", Weekly(room.Id, 1));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains(BookingRuleCodes.RoomAlreadyBooked, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_recurrence_ends_on_a_count_or_on_a_day_but_not_both()
    {
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        var room = await NewRoomAsync(facilities);

        var both = new CreateBookingRequest(room.Id, "Stand-up", Tuesday, Tuesday.AddHours(1), 4, Recurrence: new(RecurrenceFrequency.Daily, 1, 2, new DateOnly(2026, 10, 22)));
        var neither = new CreateBookingRequest(room.Id, "Stand-up", Tuesday, Tuesday.AddHours(1), 4, Recurrence: new(RecurrenceFrequency.Daily));
        var until = new CreateBookingRequest(room.Id, "Stand-up", Tuesday, Tuesday.AddHours(1), 4, Recurrence: new(RecurrenceFrequency.Daily, 1, null, new DateOnly(2026, 10, 22)));

        Assert.Equal(HttpStatusCode.BadRequest, (await facilities.PostAsJsonAsync("/api/v1/bookings", both)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await facilities.PostAsJsonAsync("/api/v1/bookings", neither)).StatusCode);
        Assert.Equal(3, (await CreatedAsync(await facilities.PostAsJsonAsync("/api/v1/bookings", until))).Series!.Created);
    }

    [Fact]
    public async Task A_series_cannot_have_more_than_52_occurrences()
    {
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        var room = await NewRoomAsync(facilities);
        var request = new CreateBookingRequest(room.Id, "Stand-up", Tuesday, Tuesday.AddHours(1), 4, Recurrence: new(RecurrenceFrequency.Daily, 1, null, new DateOnly(2028, 1, 1)));

        var response = await facilities.PostAsJsonAsync("/api/v1/bookings", request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains(BookingRuleCodes.InvalidRecurrence, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Repeating_a_series_request_with_the_same_key_returns_the_same_series()
    {
        using var api = await TestApi.StartAsync();
        using var facilities = api.ClientFor(Unique("fran"), Roles.Facilities);
        var room = await NewRoomAsync(facilities);
        var key = Unique("key");

        async Task<HttpResponseMessage> SendAsync()
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/bookings") { Content = JsonContent.Create(Weekly(room.Id, 2)) };
            message.Headers.TryAddWithoutValidation("Idempotency-Key", key);
            return await facilities.SendAsync(message);
        }

        var first = await CreatedAsync(await SendAsync());
        var second = await CreatedAsync(await SendAsync());

        Assert.Equal(first.Series!.Id, second.Series!.Id);
        Assert.Equal(first.Series.Occurrences.Select(occurrence => occurrence.BookingId), second.Series.Occurrences.Select(occurrence => occurrence.BookingId));
    }
}