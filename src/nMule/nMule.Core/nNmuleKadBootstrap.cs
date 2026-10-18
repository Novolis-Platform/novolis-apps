namespace Novolis.NMule.Core;

/// <summary>One user-editable Kad bootstrap source.</summary>
public sealed record NmuleKadBootstrap(
    string Host,
    int UdpPort = 4672,
    int TcpPort = 4662)
{
    /// <summary>Parses an IPv4 or DNS host followed by a UDP port.</summary>
    public static NmuleKadBootstrap Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var text = value.Trim();
        var separator = text.LastIndexOf(':');
        if (separator <= 0
            || separator == text.Length - 1
            || !int.TryParse(text[(separator + 1)..], out var udpPort))
        {
            throw new FormatException(
                "Kad bootstrap must use the form host:udpPort.");
        }

        if (udpPort is < 1 or > 65535)
            throw new FormatException("Kad bootstrap UDP port is outside 1-65535.");

        return new NmuleKadBootstrap(text[..separator], udpPort);
    }

    /// <summary>Formats the bootstrap host and UDP port.</summary>
    public override string ToString() => $"{Host}:{UdpPort}";
}
