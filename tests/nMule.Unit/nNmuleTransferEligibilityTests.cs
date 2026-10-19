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
    public async Task Larger_result_is_directly_downloadable_with_multipart_support()
    {
        var hit = CreateHit(
            clientId: 16_777_216,
            size: Ed2kProtocol.PartSize + 1L);

        await Assert.That(hit.Eligibility)
            .IsEqualTo(NmuleTransferEligibility.DirectHighId);
    }

    [Test]
    public async Task Empty_result_is_unavailable()
    {
        var hit = CreateHit(clientId: 16_777_216, size: 0);

        await Assert.That(hit.Eligibility)
            .IsEqualTo(NmuleTransferEligibility.InvalidSource);
    }

    [Test]
    public async Task Search_hit_exposes_hash_names_availability_and_speed()
    {
        var result = new Ed2kSearchResult(
            new Ed2kFileId(Enumerable.Repeat((byte)0x21, 16).ToArray()),
            16_777_216,
            4662,
            "renamed.bin",
            1024,
            "Archive",
            12,
            2 * 1024 * 1024,
            4,
            75);
        var hit = new NmuleSearchHit(
            result,
            ["renamed.bin", "original.bin"],
            @"C:\Users\test\AppData\Local\nMule\completed");

        await Assert.That(hit.HashLabel).IsEqualTo(hit.FileId.ToString());
        await Assert.That(hit.AvailabilityLabel)
            .IsEqualTo("12 source(s) · 75% availability");
        await Assert.That(hit.SpeedLabel).IsEqualTo("2 MiB/s");
        await Assert.That(hit.SameHashNamesLabel)
            .Contains("original.bin");
        await Assert.That(hit.OutputLocationLabel).Contains("completed");
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
