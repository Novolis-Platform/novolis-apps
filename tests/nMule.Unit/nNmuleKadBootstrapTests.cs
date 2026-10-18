using Novolis.NMule.Core;

namespace Novolis.NMule.Unit;

public sealed class NmuleKadBootstrapTests
{
    [Test]
    public async Task Parse_accepts_a_host_and_udp_port()
    {
        var bootstrap = NmuleKadBootstrap.Parse("kad.example.test:4672");

        await Assert.That(bootstrap.Host).IsEqualTo("kad.example.test");
        await Assert.That(bootstrap.UdpPort).IsEqualTo(4672);
        await Assert.That(bootstrap.TcpPort).IsEqualTo(4662);
        await Assert.That(bootstrap.ToString()).IsEqualTo("kad.example.test:4672");
    }

    [Test]
    public async Task Parse_rejects_missing_or_invalid_ports()
    {
        await Assert.That(() => NmuleKadBootstrap.Parse("kad.example.test"))
            .Throws<FormatException>();
        await Assert.That(() => NmuleKadBootstrap.Parse("kad.example.test:0"))
            .Throws<FormatException>();
        await Assert.That(() => NmuleKadBootstrap.Parse("kad.example.test:65536"))
            .Throws<FormatException>();
    }
}
