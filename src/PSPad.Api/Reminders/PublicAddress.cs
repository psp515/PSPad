using System.Net;
using System.Net.Sockets;

namespace PSPad.Api.Reminders;

public static class PublicAddress
{
    static readonly IPNetwork[] ReservedV4 = Networks(
        "0.0.0.0/8", "10.0.0.0/8", "100.64.0.0/10", "127.0.0.0/8", "169.254.0.0/16", "172.16.0.0/12",
        "192.0.0.0/24", "192.0.2.0/24", "192.88.99.0/24", "192.168.0.0/16", "198.18.0.0/15",
        "198.51.100.0/24", "203.0.113.0/24", "224.0.0.0/4", "240.0.0.0/4");

    static readonly IPNetwork[] ReservedV6 = Networks(
        "::/96", "100::/64", "2001::/32", "2001:db8::/32", "64:ff9b:1::/48", "fc00::/7", "fe80::/10",
        "fec0::/10", "ff00::/8");

    static readonly IPNetwork Nat64 = IPNetwork.Parse("64:ff9b::/96");

    static readonly IPNetwork SixToFour = IPNetwork.Parse("2002::/16");

    public static bool IsAllowed(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => !ReservedV4.Any(network => network.Contains(address)),
            AddressFamily.InterNetworkV6 => IsAllowedV6(address),
            _ => false,
        };
    }

    // NAT64 and 6to4 carry an IPv4 address inside, so the IPv4 inside decides.
    static bool IsAllowedV6(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        if (Nat64.Contains(address))
        {
            return IsAllowed(new IPAddress(bytes[12..16]));
        }

        if (SixToFour.Contains(address))
        {
            return IsAllowed(new IPAddress(bytes[2..6]));
        }

        return !ReservedV6.Any(network => network.Contains(address));
    }

    static IPNetwork[] Networks(params string[] ranges) => [.. ranges.Select(IPNetwork.Parse)];
}
