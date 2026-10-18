using Novolis.NMule.Core;

namespace Novolis.NMule.Unit;

public sealed class NmuleNetworkPolicyTests
{
    [Test]
    public async Task No_vpn_is_denied()
    {
        var decision = NmuleNetworkPolicy.Evaluate(
            NmuleVpnTransportKind.NoVpn,
            allowTailscale: false);

        await Assert.That(decision.IsAllowed).IsFalse();
        await Assert.That(decision.DenialReason).Contains("active VPN");
    }

    [Test]
    public async Task Normal_vpn_is_allowed()
    {
        var decision = NmuleNetworkPolicy.Evaluate(
            NmuleVpnTransportKind.Vpn,
            allowTailscale: false);

        await Assert.That(decision.IsAllowed).IsTrue();
        await Assert.That(decision.DenialReason).IsNull();
    }

    [Test]
    public async Task Tailscale_is_denied_without_the_session_override()
    {
        var decision = NmuleNetworkPolicy.Evaluate(
            NmuleVpnTransportKind.Tailscale,
            allowTailscale: false);

        await Assert.That(decision.IsAllowed).IsFalse();
        await Assert.That(decision.DenialReason).Contains("Tailscale");
    }

    [Test]
    public async Task Tailscale_is_allowed_only_with_the_session_override()
    {
        var decision = NmuleNetworkPolicy.Evaluate(
            NmuleVpnTransportKind.Tailscale,
            allowTailscale: true);

        await Assert.That(decision.IsAllowed).IsTrue();
    }

    [Test]
    public async Task Complete_override_allows_a_non_vpn_path()
    {
        var decision = NmuleNetworkPolicy.Evaluate(
            NmuleVpnTransportKind.NoVpn,
            allowTailscale: false,
            allowUnrestrictedNetwork: true);

        await Assert.That(decision.IsAllowed).IsTrue();
    }

    [Test]
    public async Task Complete_override_also_allows_tailscale()
    {
        var decision = NmuleNetworkPolicy.Evaluate(
            NmuleVpnTransportKind.Tailscale,
            allowTailscale: false,
            allowUnrestrictedNetwork: true);

        await Assert.That(decision.IsAllowed).IsTrue();
    }
}
