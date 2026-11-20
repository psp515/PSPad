using System.Net;
using System.Net.Sockets;

namespace PSPad.Api.Reminders;

public static class PublicAddress
{
    public static bool IsAllowed(IPAddress address)
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
            AddressFamily.InterNetwork => !IsReservedV4(bytes),
            AddressFamily.InterNetworkV6 => !(address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || (bytes[0] & 0xFE) == 0xFC),
            _ => false,
        };
    }

    static bool IsReservedV4(byte[] b) =>
        b[0] == 10
        || b[0] == 0
        || (b[0] == 172 && b[1] is >= 16 and <= 31)
        || (b[0] == 192 && b[1] == 168)
        || (b[0] == 169 && b[1] == 254)
        || (b[0] == 100 && b[1] is >= 64 and <= 127);
}
