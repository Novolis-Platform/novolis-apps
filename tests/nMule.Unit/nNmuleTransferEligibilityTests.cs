using Novolis.NMule.Core;
using Novolis.Transports.Ed2k;

namespace Novolis.NMule.Unit;

public sealed class NmuleTransferEligibilityTests
{
    [Test]
    public async Task High_id_single_part_result_is_directly_downloadable()
    {
        var hit = CreateHit(
            clientId: 16_777_216,
            size: Ed2kProtocol.PartSize);

        await Assert.That(hit.Eligibility)
            .IsEqualTo(NmuleTransferEligibility.DirectHighId);
    }

    [Test]
    public async Task Low_id_single_part_result_uses_kad_source_lookup()
    {
        var hit = CreateHit(clientId: 1, size: 1024);

        await Assert.That(hit.Eligibility)
            .IsEqualTo(NmuleTransferEligibility.KadSourceRequired);
    }

    [Test]
    public async Task Larger_result_is_rejected_until_multipart_support_exists()
    {
        var hit = CreateHit(
            clientId: 16_777_216,
            size: Ed2kProtocol.PartSize + 1L);

        await Assert.That(hit.Eligibility)
            .IsEqualTo(NmuleTransferEligibility.MultipartRequired);
    }

    [Test]
    public async Task Empty_result_is_unavailable()
    {
        var hit = CreateHit(clientId: 16_777_216, size: 0);

        await Assert.That(hit.Eligibility)
            .IsEqualTo(NmuleTransferEligibility.InvalidSource);
    }

    private static NmuleSearchHit CreateHit(uint clientId, long size) =>
        new(
            new Ed2kSearchResult(
                new Ed2kFileId(new byte[16]),
                clientId,
                4662,
                "sample.bin",
                size,
                "Archive",
                1));
}
