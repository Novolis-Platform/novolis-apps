using System.Buffers.Binary;
using System.Net;
using Novolis.Transports.Kad;

namespace Novolis.NMule.Core;

/// <summary>Reads the bounded v1/v2/v3 aMule and eMule nodes.dat formats.</summary>
public static class NmuleNodesDat
{
    /// <summary>Parses usable IPv4 Kad contacts from a nodes.dat payload.</summary>
    public static IReadOnlyList<KadContact> Parse(
        ReadOnlySpan<byte> data,
        int maximumContacts = NmuleDefaults.MaximumKadContacts)
    {
        if (data.Length < 12)
            throw new InvalidDataException("The nodes.dat payload is truncated.");
        if (maximumContacts < 1)
            throw new ArgumentOutOfRangeException(nameof(maximumContacts));

        var offset = 0;
        var header = ReadUInt32(data, ref offset);
        if (header != 0)
            throw new InvalidDataException("The nodes.dat header is invalid.");

        var version = ReadUInt32(data, ref offset);
        var recordSize = version switch
        {
            1 or 2 => 25,
            3 => ReadBootstrapEdition(data, ref offset),
            _ => throw new InvalidDataException(
                $"The nodes.dat version {version} is not supported."),
        };
        var count = ReadUInt32(data, ref offset);
        if (count > (uint)maximumContacts)
            throw new InvalidDataException("The nodes.dat contact count is too large.");
        if (count > int.MaxValue)
            throw new InvalidDataException("The nodes.dat contact count is invalid.");

        var contacts = new List<KadContact>((int)count);
        for (var index = 0; index < count; index++)
        {
            if (version == 2)
            {
                if (data.Length - offset < 34)
                    throw new InvalidDataException("The nodes.dat contact is truncated.");
            }
            else if (data.Length - offset < recordSize)
            {
                throw new InvalidDataException("The nodes.dat contact is truncated.");
            }

            var id = new KadId(data.Slice(offset, 16));
            offset += 16;
            var addressBytes = data.Slice(offset, 4).ToArray();
            offset += 4;
            var address = new IPAddress(
            [
                addressBytes[3],
                addressBytes[2],
                addressBytes[1],
                addressBytes[0],
            ]);
            var udpPort = ReadUInt16(data, ref offset);
            var tcpPort = ReadUInt16(data, ref offset);
            var protocolVersion = data[offset++];

            if (version == 2)
                offset += 9;

            if (udpPort > 0
                && tcpPort > 0
                && !IPAddress.Any.Equals(address)
                && protocolVersion > 1)
            {
                contacts.Add(
                    new KadContact(
                        id,
                        new IPEndPoint(address, udpPort),
                        tcpPort,
                        protocolVersion));
            }
        }

        return contacts;
    }

    private static int ReadBootstrapEdition(
        ReadOnlySpan<byte> data,
        ref int offset)
    {
        var edition = ReadUInt32(data, ref offset);
        if (edition > 1)
            throw new InvalidDataException("The nodes.dat bootstrap edition is invalid.");
        return 25;
    }

    private static ushort ReadUInt16(
        ReadOnlySpan<byte> data,
        ref int offset)
    {
        Require(data, offset, sizeof(ushort));
        var value = BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]);
        offset += sizeof(ushort);
        return value;
    }

    private static uint ReadUInt32(
        ReadOnlySpan<byte> data,
        ref int offset)
    {
        Require(data, offset, sizeof(uint));
        var value = BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);
        offset += sizeof(uint);
        return value;
    }

    private static void Require(
        ReadOnlySpan<byte> data,
        int offset,
        int count)
    {
        if (count < 0 || offset < 0 || count > data.Length - offset)
            throw new InvalidDataException("The nodes.dat payload is truncated.");
    }
}
