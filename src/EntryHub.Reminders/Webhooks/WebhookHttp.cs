using System.Net;
using System.Net.Sockets;

using EntryHub.Api.Domain.Webhooks;

namespace EntryHub.Reminders.Webhooks;

/// <summary>
/// The HTTP client that sends webhooks. It does not follow redirects, uses no proxy and, whatever the URL says, only
/// connects to public addresses: the check is made on the address that is really used, so a name that points to a
/// private address (or changes its answer between two lookups) cannot reach the inside of the network.
/// </summary>
public static class WebhookHttp
{
    public static HttpClient CreateClient(TimeSpan timeout)
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            UseCookies = false,
            ConnectTimeout = timeout,
            ConnectCallback = async (context, cancellationToken) => await ConnectAsync(context.DnsEndPoint, cancellationToken),
        };

        return new HttpClient(handler) { Timeout = timeout };
    }

    /// <summary>
    /// Resolves the host and connects to it, refusing when any of its addresses is not public.
    /// </summary>
    public static async ValueTask<Stream> ConnectAsync(DnsEndPoint endpoint, CancellationToken cancellationToken)
    {
        var addresses = IPAddress.TryParse(endpoint.Host.Trim('[', ']'), out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(endpoint.Host, cancellationToken);

        if (addresses.Length == 0 || addresses.Any(address => !WebhookUrlGuard.IsPublic(address)))
        {
            throw new HttpRequestException("The host does not resolve to a public address.");
        }

        Exception? last = null;

        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, endpoint.Port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception exception) when (exception is SocketException)
            {
                socket.Dispose();
                last = exception;
            }
        }

        throw new HttpRequestException("Could not connect to the host.", last);
    }
}