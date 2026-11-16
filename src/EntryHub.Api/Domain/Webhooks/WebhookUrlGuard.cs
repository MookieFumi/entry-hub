using System.Net;
using System.Net.Sockets;

namespace EntryHub.Api.Domain.Webhooks;

/// <summary>
/// Keeps webhooks from reaching the inside of a network (server-side request forgery): only <c>https</c> URLs with a
/// public host. This checks what the URL says; it does not resolve names, so a delivery has to check the address it
/// really connects to with <see cref="IsPublic"/> too.
/// </summary>
public static class WebhookUrlGuard
{
    /// <returns>Why the URL cannot be used, or <see langword="null"/> when it can.</returns>
    public static string? Validate(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return "The URL must be absolute.";
        }

        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            return "The URL must use https.";
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return "The URL cannot contain a user name or password.";
        }

        var host = uri.IdnHost.TrimEnd('.');

        if (IPAddress.TryParse(host.Trim('[', ']'), out var address))
        {
            return IsPublic(address) ? null : "The URL cannot point to a loopback, private or link-local address.";
        }

        if (!host.Contains('.', StringComparison.Ordinal) || IsLocalName(host))
        {
            return "The URL must use a public host name.";
        }

        return null;
    }

    /// <summary>
    /// False for loopback, private, link-local, shared (carrier-grade NAT), multicast, reserved and unspecified
    /// addresses, including IPv4 addresses written as IPv6.
    /// </summary>
    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            return false;
        }

        var bytes = address.GetAddressBytes();

        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => IsPublicV4(bytes),
            AddressFamily.InterNetworkV6 => IsPublicV6(address, bytes),
            _ => false,
        };
    }

    private static bool IsLocalName(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith(".lan", StringComparison.OrdinalIgnoreCase);

    private static bool IsPublicV4(byte[] b) =>
        !(b[0] == 0
            || b[0] == 10
            || (b[0] == 100 && b[1] is >= 64 and <= 127)
            || b[0] == 127
            || (b[0] == 169 && b[1] == 254)
            || (b[0] == 172 && b[1] is >= 16 and <= 31)
            || (b[0] == 192 && b[1] == 0 && b[2] == 0)
            || (b[0] == 192 && b[1] == 168)
            || (b[0] == 198 && b[1] is 18 or 19)
            || b[0] >= 224);

    private static bool IsPublicV6(IPAddress address, byte[] b) =>
        !(address.IsIPv6LinkLocal
            || address.IsIPv6SiteLocal
            || address.IsIPv6Multicast
            || (b[0] & 0xFE) == 0xFC);
}