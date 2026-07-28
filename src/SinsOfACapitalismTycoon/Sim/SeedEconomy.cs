using Novolis.Economy.Core;
using Novolis.Economy.Core.Holdings;
using Novolis.Economy.Core.Transport;

namespace SinsOfACapitalismTycoon.Sim;

internal sealed record SeedKnobs(
    decimal MineOutputPerRun,
    decimal MineInstalled,
    decimal FactoryInstalled,
    decimal LaneCapacity,
    int LaneTravelPeriods,
    decimal MineLogisticsCapacity,
    Money FactoryCash,
    Money MinerCash,
    Money HouseholdCash,
    Money StateCash,
    Money BankCash,
    Money InsurerCash,
    decimal OreOpeningAtMine,
    decimal OreOpeningAtFactory,
    Money TransferPerHousehold,
    decimal HouseholdTaxRate,
    decimal FirmTaxRate,
    Money WagePerHour,
    decimal OreUnitPrice,
    decimal HaulTargetBuffer,
    decimal HaulMaxPerPeriod,
    decimal WageCashReserve,
    bool IncludeCreditFacility,
    Money FacilityLimit,
    bool IncludeInsurance,
    Money InsurancePremium,
    Money InsuranceDeductible)
{
    public static SeedKnobs Baseline { get; } = new(
        MineOutputPerRun: 6m,
        MineInstalled: 5m,
        FactoryInstalled: 4m,
        LaneCapacity: 30m,
        LaneTravelPeriods: 2,
        MineLogisticsCapacity: 40m,
        FactoryCash: Money.From(180m),
        MinerCash: Money.From(120m),
        HouseholdCash: Money.From(500m),
        StateCash: Money.From(5_000m),
        BankCash: Money.From(200m),
        InsurerCash: Money.From(300m),
        OreOpeningAtMine: 20m,
        OreOpeningAtFactory: 8m,
        TransferPerHousehold: Money.From(0.5m),
        HouseholdTaxRate: 0.02m,
        FirmTaxRate: 0.01m,
        WagePerHour: Money.From(1.2m),
        OreUnitPrice: 2m,
        HaulTargetBuffer: 24m,
        HaulMaxPerPeriod: 20m,
        WageCashReserve: 0m,
        IncludeCreditFacility: false,
        FacilityLimit: Money.Zero,
        IncludeInsurance: false,
        InsurancePremium: Money.Zero,
        InsuranceDeductible: Money.Zero);
}

/// <summary>Two-region Core smoke builder parameterized by scenario knobs.</summary>
internal static class SeedEconomy
{
    public static (EconomyState State, SeedIds Ids, SeedKnobs Knobs) Create(ulong seed, SeedKnobs knobs)
    {
        var ids = SeedIds.FromSeed(seed);

        var mineRecipe = new ActivityRecipe(
            Inputs: [],
            Outputs: [new ResourceAmount(ids.OreId, knobs.MineOutputPerRun)],
            LaborHoursPerRun: 3m,
            ProductionSpacePerRun: 1m);

        var factoryRecipe = new ActivityRecipe(
            [new ResourceAmount(ids.OreId, 2m)],
            [new ResourceAmount(ids.WidgetId, 2m)],
            LaborHoursPerRun: 4m,
            ProductionSpacePerRun: 1m);

        var mine = new Region(
            ids.MineRegion, LivingCapacity: 200, ProductionCapacity: 80m, knobs.MineLogisticsCapacity);
        var factory = new Region(
            ids.FactoryRegion, LivingCapacity: 200, ProductionCapacity: 80m, LogisticsCapacity: 40m);

        var factoryCohort = new HouseholdCohort(
            ids.FactoryCohortId,
            ids.FactoryRegion,
            HouseholdCount: 24,
            new HouseholdProfile(0.55m, 0.15m, 1m, 0m),
            HouseholdLaborKind.Mean,
            CashPerHousehold: Money.From(40m),
            HouseholdEntityId: ids.HouseholdId);

        var mineCohort = new HouseholdCohort(
            ids.MineCohortId,
            ids.MineRegion,
            HouseholdCount: 12,
            new HouseholdProfile(0.35m, 0.2m, 1m, 0m),
            HouseholdLaborKind.Common,
            CashPerHousehold: Money.From(20m),
            HouseholdEntityId: ids.HouseholdId);

        var lane = new TransportLane(
            ids.MineRegion, ids.FactoryRegion, knobs.LaneTravelPeriods, knobs.LaneCapacity);

        var entities = new Dictionary<LegalEntityId, LegalEntity>
        {
            [ids.MinerFirm] = new LegalEntity(ids.MinerFirm, LegalEntityKind.Firm, knobs.MinerCash),
            [ids.FactoryFirm] = new LegalEntity(ids.FactoryFirm, LegalEntityKind.Firm, knobs.FactoryCash),
            [ids.HouseholdId] = new LegalEntity(ids.HouseholdId, LegalEntityKind.Household, knobs.HouseholdCash),
            [ids.StateId] = new LegalEntity(ids.StateId, LegalEntityKind.State, knobs.StateCash),
            [ids.BankId] = new LegalEntity(ids.BankId, LegalEntityKind.Bank, knobs.BankCash),
            [ids.InsurerId] = new LegalEntity(ids.InsurerId, LegalEntityKind.Insurer, knobs.InsurerCash)
        };

        var state = EconomyState.Empty with
        {
            Entities = entities,
            Regions = new Dictionary<RegionId, Region>
            {
                [ids.MineRegion] = mine,
                [ids.FactoryRegion] = factory
            },
            Cohorts = new Dictionary<CohortId, HouseholdCohort>
            {
                [ids.FactoryCohortId] = factoryCohort,
                [ids.MineCohortId] = mineCohort
            },
            Activities = new Dictionary<ActivityId, Activity>
            {
                [ids.MineActivityId] = new Activity(
                    ids.MineActivityId, ids.MinerFirm, ids.MineRegion, mineRecipe, knobs.MineInstalled),
                [ids.FactoryActivityId] = new Activity(
                    ids.FactoryActivityId, ids.FactoryFirm, ids.FactoryRegion, factoryRecipe, knobs.FactoryInstalled)
            },
            Resources = new Dictionary<ResourceId, Resource>
            {
                [ids.OreId] = new Resource(ids.OreId, "Ore", ResourceKind.IntermediateGood),
                [ids.WidgetId] = new Resource(ids.WidgetId, "Widget", ResourceKind.ConsumerGood)
            },
            Lanes = new Dictionary<string, TransportLane>
            {
                [TransferEngine.LaneKey(ids.MineRegion, ids.FactoryRegion)] = lane
            },
            PostedPrices = new Dictionary<string, PostedPrice>
            {
                [EconomyState.PriceKey(ids.FactoryRegion, ids.WidgetId)] =
                    new PostedPrice(ids.FactoryRegion, ids.WidgetId, Money.From(6m))
            },
            Policy = new StatePolicy(
                knobs.HouseholdTaxRate,
                knobs.FirmTaxRate,
                knobs.TransferPerHousehold,
                DepositReserveRequirement: 0m,
                InsuranceCapitalRequirement: 0m,
                knobs.WagePerHour)
        };

        state = HoldingLedger.Credit(state, ids.MinerFirm, ids.MineRegion, ids.OreId, knobs.OreOpeningAtMine);
        state = HoldingLedger.Credit(state, ids.FactoryFirm, ids.FactoryRegion, ids.OreId, knobs.OreOpeningAtFactory);

        if (knobs.IncludeCreditFacility)
        {
            state = state with
            {
                CreditFacilities = new Dictionary<CreditFacilityId, CreditFacility>
                {
                    [ids.FactoryFacilityId] = new CreditFacility(
                        ids.FactoryFacilityId,
                        ids.BankId,
                        ids.FactoryFirm,
                        knobs.FacilityLimit,
                        Drawn: Money.Zero,
                        IsCommitted: true)
                }
            };
        }

        if (knobs.IncludeInsurance)
        {
            state = state with
            {
                Insurance =
                [
                    new InsuranceCoverage(
                        ids.InsurerId,
                        ids.FactoryFirm,
                        RiskKind.ProductionLoss,
                        CoveredFraction: 0.8m,
                        knobs.InsuranceDeductible,
                        knobs.InsurancePremium)
                ]
            };
        }

        return (state, ids, knobs);
    }
}
