using Novolis.Economy.Core;

namespace SinsOfACapitalismTycoon.Sim;

/// <summary>Stable ids for the seeded polity (derived from run seed).</summary>
internal sealed record SeedIds(
    RegionId MineRegion,
    RegionId FactoryRegion,
    LegalEntityId MinerFirm,
    LegalEntityId FactoryFirm,
    LegalEntityId HouseholdId,
    LegalEntityId StateId,
    LegalEntityId BankId,
    LegalEntityId InsurerId,
    ResourceId OreId,
    ResourceId WidgetId,
    ActivityId MineActivityId,
    ActivityId FactoryActivityId,
    CohortId FactoryCohortId,
    CohortId MineCohortId,
    CreditFacilityId FactoryFacilityId)
{
    public static SeedIds FromSeed(ulong seed) =>
        new(
            MineRegion: RegionId.From(GuidFrom(seed, 1)),
            FactoryRegion: RegionId.From(GuidFrom(seed, 2)),
            MinerFirm: LegalEntityId.From(GuidFrom(seed, 3)),
            FactoryFirm: LegalEntityId.From(GuidFrom(seed, 4)),
            HouseholdId: LegalEntityId.From(GuidFrom(seed, 5)),
            StateId: LegalEntityId.From(GuidFrom(seed, 6)),
            BankId: LegalEntityId.From(GuidFrom(seed, 7)),
            InsurerId: LegalEntityId.From(GuidFrom(seed, 8)),
            OreId: ResourceId.From(GuidFrom(seed, 9)),
            WidgetId: ResourceId.From(GuidFrom(seed, 10)),
            MineActivityId: ActivityId.From(GuidFrom(seed, 11)),
            FactoryActivityId: ActivityId.From(GuidFrom(seed, 12)),
            FactoryCohortId: CohortId.From(GuidFrom(seed, 13)),
            MineCohortId: CohortId.From(GuidFrom(seed, 14)),
            FactoryFacilityId: CreditFacilityId.From(GuidFrom(seed, 15)));

    internal static Guid GuidFrom(ulong seed, int salt)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes[..8], seed);
        BitConverter.TryWriteBytes(bytes[8..], (ulong)(uint)salt * 0x9E3779B97F4A7C15UL ^ seed);
        return new Guid(bytes);
    }
}
