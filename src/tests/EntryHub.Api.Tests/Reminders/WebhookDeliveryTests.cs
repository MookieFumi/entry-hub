using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using EntryHub.Api.Domain.Webhooks;
using EntryHub.Api.Features.V1.Bookings;
using EntryHub.Api.Features.V1.Bookings.CreateBooking;
using EntryHub.Api.Features.V1.Bookings.RescheduleBooking;
using EntryHub.Api.Features.V1.Common;
using EntryHub.Api.Features.V1.Rooms;
using EntryHub.Api.Features.V1.Rooms.CreateRoom;
using EntryHub.Api.Features.V1.Webhooks;
using EntryHub.Api.Security;
using EntryHub.Api.Tests.Support;
using EntryHub.Reminders.Webhooks;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Npgsql;

namespace EntryHub.Api.Tests.Reminders;

/// <summary>
/// Webhook events from the booking change to the signed request, against the real database and a fake receiver.
/// </summary>
[Trait("Category", "Integration")]
public class WebhookDeliveryTests
{
    private static readonly DateTimeOffset Tuesday = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private sealed record Sent(string Url, Dictionary<string, string> Headers, string Body);

    private sealed class FakeReceiver(Func<Uri, HttpStatusCode>? respond = null) : HttpMessageHandler
    {
        public ConcurrentQueue<Sent> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var headers = request.Headers.ToDictionary(header => header.Key, header => header.Value.Single());
            Requests.Enqueue(new Sent(request.RequestUri!.ToString(), headers, await request.Content!.ReadAsStringAsync(cancellationToken)));

            return new HttpResponseMessage((respond ?? (_ => HttpStatusCode.OK))(request.RequestUri));
        }

        public List<Sent> To(string url) => [.. Requests.Where(sent => sent.Url == url)];
    }

    private sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed record Scenario(TestApi Api, Guid Tenant, HttpClient Facilities, HttpClient Employee, RoomResponse Room);

    private static async Task<Scenario> StartAsync()
    {
        var tenant = await TestDatabase.CreateTenantAsync(Unique("tenant"));
        var api = await TestApi.StartAsync();
        var facilities = api.ClientForTenant(tenant, Unique("fran"), Roles.Facilities);
        var employee = api.ClientForTenant(tenant, Unique("ana"), Roles.Employee);
        var room = (await (await facilities.PostAsJsonAsync("/api/v1/rooms", new CreateRoomRequest(Unique("Room"), 1, 8, null))).Content.ReadFromJsonAsync<RoomResponse>(TestJson.Options))!;

        return new(api, tenant, facilities, employee, room);
    }

    private static async Task<WebhookCreatedResponse> SubscribeAsync(Scenario scenario, params string[] events)
    {
        var response = await scenario.Facilities.PostAsJsonAsync(
            "/api/v1/webhooks",
            new CreateWebhookRequest($"https://hooks.example.com/{Guid.NewGuid():N}", events.Length == 0 ? WebhookEvents.All : events));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WebhookCreatedResponse>(TestJson.Options))!;
    }

    private static async Task<BookingResponse> BookAsync(Scenario scenario, int hourOffset = 0)
    {
        var start = Tuesday.AddHours(hourOffset);
        var response = await scenario.Employee.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(scenario.Room.Id, "Planning", start, start.AddHours(1), 4));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BookingResponse>(TestJson.Options))!;
    }

    private static async Task<List<WebhookDeliveryResponse>> DeliveriesAsync(Scenario scenario, Guid webhookId) =>
        [.. (await scenario.Facilities.GetFromJsonAsync<PagedResponse<WebhookDeliveryResponse>>($"/api/v1/webhooks/{webhookId}/deliveries", TestJson.Options))!.Items.OrderBy(delivery => delivery.CreatedAt).ThenBy(delivery => delivery.Id)];

    private static (WebhookWorker Worker, ManualClock Clock) NewWorker(FakeReceiver receiver, WebhookOptions? options = null)
    {
        var clock = new ManualClock(TestClock.MondayMorning.AddHours(2));
        var settings = options ?? new WebhookOptions();
        var worker = new WebhookWorker(
            new WebhookDeliveryStore(NpgsqlDataSource.Create(TestDatabase.ConnectionString)),
            new WebhookSender(new HttpClient(receiver), clock),
            Options.Create(settings),
            clock,
            NullLogger<WebhookWorker>.Instance);

        return (worker, clock);
    }

    [Fact]
    public async Task A_booking_change_saves_a_delivery_for_each_subscription_that_wants_the_event()
    {
        var scenario = await StartAsync();
        using var api = scenario.Api;
        using var facilities = scenario.Facilities;
        using var employee = scenario.Employee;
        var all = await SubscribeAsync(scenario);
        var onlyCancelled = await SubscribeAsync(scenario, WebhookEvents.BookingCancelled);
        var paused = await SubscribeAsync(scenario);
        (await facilities.PatchAsJsonAsync($"/api/v1/webhooks/{paused.Id}", new UpdateWebhookRequest(false))).EnsureSuccessStatusCode();

        var booking = await BookAsync(scenario);

        Assert.Equal([WebhookEvents.BookingCreated], (await DeliveriesAsync(scenario, all.Id)).Select(delivery => delivery.EventType));
        Assert.Empty(await DeliveriesAsync(scenario, onlyCancelled.Id));
        Assert.Empty(await DeliveriesAsync(scenario, paused.Id));

        (await employee.PostAsync($"/api/v1/bookings/{booking.Id}/cancel", null)).EnsureSuccessStatusCode();

        Assert.Equal([WebhookEvents.BookingCancelled], (await DeliveriesAsync(scenario, onlyCancelled.Id)).Select(delivery => delivery.EventType));
        Assert.Equal(2, (await DeliveriesAsync(scenario, all.Id)).Count);
    }

    [Fact]
    public async Task A_rescheduled_booking_saves_a_rescheduled_event()
    {
        var scenario = await StartAsync();
        using var api = scenario.Api;
        using var facilities = scenario.Facilities;
        using var employee = scenario.Employee;
        var webhook = await SubscribeAsync(scenario, WebhookEvents.BookingRescheduled);
        var booking = await BookAsync(scenario);

        (await employee.PostAsJsonAsync($"/api/v1/bookings/{booking.Id}/reschedule", new RescheduleBookingRequest(Tuesday.AddHours(2), Tuesday.AddHours(3)))).EnsureSuccessStatusCode();

        Assert.Equal([WebhookEvents.BookingRescheduled], (await DeliveriesAsync(scenario, webhook.Id)).Select(delivery => delivery.EventType));
    }

    [Fact]
    public async Task The_worker_sends_the_event_signed_with_the_secret_of_the_subscription()
    {
        var scenario = await StartAsync();
        using var api = scenario.Api;
        using var facilities = scenario.Facilities;
        using var employee = scenario.Employee;
        var webhook = await SubscribeAsync(scenario, WebhookEvents.BookingCreated);
        var booking = await BookAsync(scenario);
        var receiver = new FakeReceiver();
        var (worker, clock) = NewWorker(receiver);

        await worker.DeliverAsync(CancellationToken.None);

        var sent = Assert.Single(receiver.To(webhook.Url));
        Assert.Equal(WebhookEvents.BookingCreated, sent.Headers["X-EntryHub-Event"]);
        Assert.Equal(clock.Now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), sent.Headers["X-EntryHub-Timestamp"]);
        Assert.Equal(WebhookSender.Sign(webhook.Secret, sent.Headers["X-EntryHub-Timestamp"], sent.Body), sent.Headers["X-EntryHub-Signature"]);
        Assert.NotEqual(WebhookSender.Sign("whsec_wrong", sent.Headers["X-EntryHub-Timestamp"], sent.Body), sent.Headers["X-EntryHub-Signature"]);

        using var body = JsonDocument.Parse(sent.Body);
        Assert.Equal(WebhookEvents.BookingCreated, body.RootElement.GetProperty("type").GetString());
        Assert.Equal(booking.Id, body.RootElement.GetProperty("data").GetProperty("id").GetGuid());
        Assert.Equal("Confirmed", body.RootElement.GetProperty("data").GetProperty("status").GetString());

        var delivery = Assert.Single(await DeliveriesAsync(scenario, webhook.Id));
        Assert.Equal((WebhookDeliveryStatus.Succeeded, 1, 200), (delivery.Status, delivery.Attempts, delivery.LastStatusCode));
        Assert.Equal(body.RootElement.GetProperty("id").GetGuid(), delivery.EventId);

        await worker.DeliverAsync(CancellationToken.None);
        Assert.Single(receiver.To(webhook.Url));
    }

    [Fact]
    public async Task A_failed_delivery_is_retried_with_a_growing_delay_and_then_given_up()
    {
        var scenario = await StartAsync();
        using var api = scenario.Api;
        using var facilities = scenario.Facilities;
        using var employee = scenario.Employee;
        var webhook = await SubscribeAsync(scenario, WebhookEvents.BookingCreated);
        await BookAsync(scenario);
        var receiver = new FakeReceiver(_ => HttpStatusCode.InternalServerError);
        var options = new WebhookOptions { MaxAttempts = 3, BaseDelay = TimeSpan.FromSeconds(30) };
        var (worker, clock) = NewWorker(receiver, options);
        var first = clock.Now;

        await worker.DeliverAsync(CancellationToken.None);
        var afterFirst = Assert.Single(await DeliveriesAsync(scenario, webhook.Id));
        Assert.Equal((WebhookDeliveryStatus.Pending, 1, 500), (afterFirst.Status, afterFirst.Attempts, afterFirst.LastStatusCode));
        Assert.Equal(first.AddSeconds(30), afterFirst.NextAttemptAt);

        clock.Now = first.AddSeconds(29);
        await worker.DeliverAsync(CancellationToken.None);
        Assert.Single(receiver.To(webhook.Url));

        clock.Now = first.AddSeconds(30);
        await worker.DeliverAsync(CancellationToken.None);
        var afterSecond = Assert.Single(await DeliveriesAsync(scenario, webhook.Id));
        Assert.Equal((1 + 1, first.AddSeconds(30 + 60)), (afterSecond.Attempts, afterSecond.NextAttemptAt));

        clock.Now = first.AddSeconds(90);
        await worker.DeliverAsync(CancellationToken.None);
        var last = Assert.Single(await DeliveriesAsync(scenario, webhook.Id));
        Assert.Equal((WebhookDeliveryStatus.Failed, 3), (last.Status, last.Attempts));
        Assert.NotNull(last.CompletedAt);
        Assert.Equal("The receiver answered 500.", last.LastError);

        clock.Now = first.AddDays(1);
        await worker.DeliverAsync(CancellationToken.None);
        Assert.Equal(3, receiver.To(webhook.Url).Count);
    }

    [Fact]
    public async Task A_redirect_is_a_failure_and_is_not_followed()
    {
        var scenario = await StartAsync();
        using var api = scenario.Api;
        using var facilities = scenario.Facilities;
        using var employee = scenario.Employee;
        var webhook = await SubscribeAsync(scenario, WebhookEvents.BookingCreated);
        await BookAsync(scenario);
        var receiver = new FakeReceiver(_ => HttpStatusCode.Redirect);
        var (worker, _) = NewWorker(receiver);

        await worker.DeliverAsync(CancellationToken.None);

        var delivery = Assert.Single(await DeliveriesAsync(scenario, webhook.Id));
        Assert.Equal((WebhookDeliveryStatus.Pending, 302), (delivery.Status, delivery.LastStatusCode));
        Assert.Contains("redirects are not followed", delivery.LastError);
    }

    [Fact]
    public async Task Deliveries_of_a_disabled_subscription_are_given_up_without_sending()
    {
        var scenario = await StartAsync();
        using var api = scenario.Api;
        using var facilities = scenario.Facilities;
        using var employee = scenario.Employee;
        var webhook = await SubscribeAsync(scenario, WebhookEvents.BookingCreated);
        await BookAsync(scenario);
        (await facilities.PatchAsJsonAsync($"/api/v1/webhooks/{webhook.Id}", new UpdateWebhookRequest(false))).EnsureSuccessStatusCode();
        var receiver = new FakeReceiver();
        var (worker, _) = NewWorker(receiver);

        await worker.DeliverAsync(CancellationToken.None);

        Assert.Empty(receiver.To(webhook.Url));
        var delivery = Assert.Single(await DeliveriesAsync(scenario, webhook.Id));
        Assert.Equal((WebhookDeliveryStatus.Failed, "The subscription is disabled."), (delivery.Status, delivery.LastError));
    }

    [Fact]
    public async Task Two_workers_never_send_the_same_delivery()
    {
        var scenario = await StartAsync();
        using var api = scenario.Api;
        using var facilities = scenario.Facilities;
        using var employee = scenario.Employee;
        var webhook = await SubscribeAsync(scenario, WebhookEvents.BookingCreated);

        for (var hour = 0; hour < 5; hour++)
        {
            await BookAsync(scenario, hour * 2);
        }

        var receiver = new FakeReceiver();
        var (first, _) = NewWorker(receiver);
        var (second, _) = NewWorker(receiver);

        await Task.WhenAll(first.DeliverAsync(CancellationToken.None), second.DeliverAsync(CancellationToken.None));

        var sent = receiver.To(webhook.Url);
        Assert.Equal(5, sent.Count);
        Assert.Equal(5, sent.Select(request => request.Headers["X-EntryHub-Delivery"]).Distinct().Count());
        Assert.All(await DeliveriesAsync(scenario, webhook.Id), delivery => Assert.Equal(WebhookDeliveryStatus.Succeeded, delivery.Status));
    }

    [Fact]
    public async Task Another_tenant_cannot_read_the_deliveries_of_a_subscription()
    {
        var scenario = await StartAsync();
        using var api = scenario.Api;
        using var facilities = scenario.Facilities;
        using var employee = scenario.Employee;
        var webhook = await SubscribeAsync(scenario);
        using var other = api.ClientForTenant(await TestDatabase.CreateTenantAsync(Unique("tenant")), Unique("fran"), Roles.Facilities);

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/webhooks/{webhook.Id}/deliveries")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync($"/api/v1/webhooks/{webhook.Id}/deliveries")).StatusCode);
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("127.0.0.1")]
    [InlineData("169.254.169.254")]
    [InlineData("10.0.0.5")]
    [InlineData("::1")]
    public async Task The_client_refuses_to_connect_to_addresses_that_are_not_public(string host)
    {
        var error = await Assert.ThrowsAsync<HttpRequestException>(async () => await WebhookHttp.ConnectAsync(new DnsEndPoint(host, 443), CancellationToken.None));

        Assert.Contains("public address", error.Message);
    }

    [Fact]
    public async Task The_sender_checks_the_url_again_before_sending()
    {
        var receiver = new FakeReceiver();
        var sender = new WebhookSender(new HttpClient(receiver), TestClock.Default);

        var result = await sender.SendAsync(new DueDelivery(Guid.NewGuid(), Guid.NewGuid(), WebhookEvents.BookingCreated, "{}", 0, "https://10.0.0.5/hook", "whsec_x"), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Empty(receiver.Requests);
    }
}