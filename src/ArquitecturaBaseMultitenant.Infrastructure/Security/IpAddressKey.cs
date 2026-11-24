using System.Net;
using System.Net.Sockets;

namespace ArquitecturaBaseMultitenant.Infrastructure.Security;

/// <summary>
/// La forma canónica de una dirección antes de su HMAC (N20, ADR 0063): una IPv4, también escrita como IPv6, queda como
/// IPv4, y una IPv6 cuenta por su /64, que es lo que suele tener una sola conexión: cambiar los últimos 64 bits no da otra
/// cuenta. Sin una dirección legible, una clave fija.
/// </summary>
internal static class IpAddressKey
{
    internal const string Unknown = "unknown";

    private const int PrefixBytes = 8;

    public static string Normalize(string? ipAddress)
    {
        if (!IPAddress.TryParse(ipAddress, out var address)) return Unknown;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (address.AddressFamily != AddressFamily.InterNetworkV6) return address.ToString();
        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, PrefixBytes, bytes.Length - PrefixBytes);
        return new IPAddress(bytes).ToString() + "/64";
    }
}
