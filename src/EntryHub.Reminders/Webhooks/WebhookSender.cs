using System.Net;
using System.Security.Cryptography;
using System.Text;

using EntryHub.Api.Domain.Webhooks;

namespace EntryHub.Reminders.Webhooks;

/// <summary>
/// A delivery that is due, with where to send it and the key to sign it.
/// </summary>
public sealed record DueDelivery(Guid Id, Guid EventId, string EventType, string Payload, int Attempts, string Url, string Secret);

/// <param name="Succeeded">True when the receiver answered with a 2xx status.</param>
/// <param name="StatusCode">What the receiver answered, if it did.</param>
/// <param name="Error">Why it failed (never the body of the answer).</param>
public sealed record SendResult(bool Succeeded, int? StatusCode, string? Error);

/// <summary>
/// Sends one webhook request. Every request carries these headers:
/// <c>X-EntryHub-Event</c> (event name), <c>X-EntryHub-Delivery</c> (id of the delivery),
/// <c>X-EntryHub-Timestamp</c> (Unix seconds) and <c>X-EntryHub-Signature</c>, which is <c>v1=</c> followed by the
/// lowercase hex HMAC-SHA256 of <c>{timestamp}.{body}</c> with the secret of the subscription.
/// </summary>
public sealed class WebhookSender(HttpClient client, TimeProvider clock)
{
    public async Task<SendResult> SendAsync(DueDelivery delivery, CancellationToken cancellationToken)
    {
        // The URL is checked again here: it was valid when it was stored, and the guard may be stricter now.
        if (WebhookUrlGuard.Validate(delivery.Url) is { } problem)
        {
            return new SendResult(false, null, problem);
        }

        var timestamp = clock.GetUtcNow().ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);

        using var request = new HttpRequestMessage(HttpMethod.Post, delivery.Url)
        {
            Content = new StringContent(delivery.Payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("X-EntryHub-Event", delivery.EventType);
        request.Headers.TryAddWithoutValidation("X-EntryHub-Delivery", delivery.Id.ToString());
        request.Headers.TryAddWithoutValidation("X-EntryHub-Timestamp", timestamp);
        request.Headers.TryAddWithoutValidation("X-EntryHub-Signature", Sign(delivery.Secret, timestamp, delivery.Payload));

        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var status = (int)response.StatusCode;

            return response.IsSuccessStatusCode
                ? new SendResult(true, status, null)
                : new SendResult(false, status, response.StatusCode is >= HttpStatusCode.MultipleChoices and < HttpStatusCode.BadRequest
                    ? $"The receiver answered {status}: redirects are not followed."
                    : $"The receiver answered {status}.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new SendResult(false, null, "The receiver did not answer in time.");
        }
        catch (HttpRequestException exception)
        {
            return new SendResult(false, null, Truncate(exception.Message));
        }
    }

    public static string Sign(string secret, string timestamp, string payload)
    {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        return "v1=" + Convert.ToHexStringLower(hash);
    }

    private static string Truncate(string message) => message.Length <= 200 ? message : message[..200];
}