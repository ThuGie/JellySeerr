using System.Net;
using System.Net.Sockets;

namespace Jellyfin.Plugin.JellySeerr.Helpers;

public static class LocalNetworkAccessHelper
{
    public static bool CanOpenLocalServices(IPAddress? clientIp, params string?[] serviceUrls)
    {
        if (clientIp == null)
        {
            return false;
        }

        if (clientIp.IsIPv4MappedToIPv6)
        {
            clientIp = clientIp.MapToIPv4();
        }

        if (!IsPrivateOrLocal(clientIp))
        {
            return false;
        }

        List<IPAddress> literalPrivateHosts = new();
        foreach (string? url in serviceUrls)
        {
            if (TryGetLiteralPrivateHost(url, out IPAddress host))
            {
                literalPrivateHosts.Add(host);
            }
        }

        if (literalPrivateHosts.Count == 0)
        {
            return true;
        }

        return literalPrivateHosts.Any(host => SharesPrivateRange(clientIp, host));
    }

    public static bool IsPrivateOrLocal(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal)
            {
                return true;
            }

            byte[] bytes = ip.GetAddressBytes();
            return bytes.Length > 0 && (bytes[0] & 0xfe) == 0xfc;
        }

        if (ip.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        byte[] v4 = ip.GetAddressBytes();
        return v4[0] == 10
            || (v4[0] == 172 && v4[1] >= 16 && v4[1] <= 31)
            || (v4[0] == 192 && v4[1] == 168);
    }

    private static bool SharesPrivateRange(IPAddress client, IPAddress service)
    {
        if (client.AddressFamily != service.AddressFamily)
        {
            return IsPrivateOrLocal(client) && IsPrivateOrLocal(service);
        }

        if (client.AddressFamily != AddressFamily.InterNetwork)
        {
            return true;
        }

        byte[] a = client.GetAddressBytes();
        byte[] b = service.GetAddressBytes();
        return a[0] == b[0] && a[1] == b[1];
    }

    private static bool TryGetLiteralPrivateHost(string? url, out IPAddress host)
    {
        host = IPAddress.None;
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        if (!IPAddress.TryParse(uri.Host, out IPAddress? parsed))
        {
            return false;
        }

        if (parsed.IsIPv4MappedToIPv6)
        {
            parsed = parsed.MapToIPv4();
        }

        if (!IsPrivateOrLocal(parsed))
        {
            return false;
        }

        host = parsed;
        return true;
    }
}
