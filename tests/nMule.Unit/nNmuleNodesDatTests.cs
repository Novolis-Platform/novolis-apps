using System.Buffers.Binary;
using Novolis.NMule.Core;

namespace Novolis.NMule.Unit;

public sealed class NmuleNodesDatTests
{
    [Test]
    public async Task Version_one_payload_returns_ipv4_contact()
    {
        var payload = CreatePayload(version: 1, includeVersionTwoPadding: false);

        var contacts = NmuleNodesDat.Parse(payload);

        await Assert.That(contacts).Count().IsEqualTo(1);
        await Assert.That(contacts[0].Endpoint.Address.ToString())
            .IsEqualTo("1.2.3.4");
        await Assert.That(contacts[0].Endpoint.Port).IsEqualTo(4672);
        await Assert.That(contacts[0].TcpPort).IsEqualTo(4662);
    }

    [Test]
    public async Task Version_two_payload_skips_the_extended_contact_fields()
    {
        var payload = CreatePayload(version: 2, includeVersionTwoPadding: true);

        var contacts = NmuleNodesDat.Parse(payload);

        await Assert.That(contacts).Count().IsEqualTo(1);
        await Assert.That(contacts[0].Endpoint.Address.ToString())
            .IsEqualTo("1.2.3.4");
    }

    [Test]
    public async Task Invalid_header_is_rejected()
    {
        var payload = CreatePayload(version: 1, includeVersionTwoPadding: false);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(), 1);

        await Assert.That(() => NmuleNodesDat.Parse(payload))
            .Throws<InvalidDataException>();
    }

    private static byte[] CreatePayload(
        uint version,
        bool includeVersionTwoPadding)
    {
        var payload = new byte[12 + (includeVersionTwoPadding ? 34 : 25)];
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(0, 4), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4, 4), version);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(8, 4), 1);

        var offset = 12;
        for (var index = 0; index < 16; index++)
            payload[offset + index] = checked((byte)(index + 1));
        offset += 16;
        payload[offset++] = 4;
        payload[offset++] = 3;
        payload[offset++] = 2;
        payload[offset++] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(
            payload.AsSpan(offset, 2),
            4672);
        offset += 2;
        BinaryPrimitives.WriteUInt16LittleEndian(
            payload.AsSpan(offset, 2),
            4662);
        offset += 2;
        payload[offset++] = 8;

        if (includeVersionTwoPadding)
            Array.Clear(payload, offset, 9);

        return payload;
    }
}
