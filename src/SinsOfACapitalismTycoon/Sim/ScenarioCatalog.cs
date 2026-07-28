using Novolis.Economy.Core;
using SinsOfACapitalismTycoon.Sim.Policies;

namespace SinsOfACapitalismTycoon.Sim;

internal sealed record ScenarioPack(
    ScenarioKind Kind,
    SeedKnobs Knobs,
    Func<PolicyCounters, SeedKnobs, IHostPolicy> CreatePolicy);

internal static class ScenarioCatalog
{
    public static ScenarioPack Get(ScenarioKind kind) =>
        kind switch
        {
            ScenarioKind.Baseline => new(
                ScenarioKind.Baseline,
                SeedKnobs.Baseline,
                (c, k) => new OreHaulPolicy(c, k.OreUnitPrice, k.HaulTargetBuffer, k.HaulMaxPerPeriod, k.WageCashReserve)),

            ScenarioKind.LogisticsBind => new(
                ScenarioKind.LogisticsBind,
                SeedKnobs.Baseline with
                {
                    MineOutputPerRun = 10m,
                    MineInstalled = 6m,
                    LaneCapacity = 5m,
                    LaneTravelPeriods = 3,
                    MineLogisticsCapacity = 5m,
                    HaulMaxPerPeriod = 5m,
                    HaulTargetBuffer = 20m,
                    OreOpeningAtFactory = 4m
                },
                (c, k) => new OreHaulPolicy(c, k.OreUnitPrice, k.HaulTargetBuffer, k.HaulMaxPerPeriod, k.WageCashReserve)),

            ScenarioKind.WorkingCapital => new(
                ScenarioKind.WorkingCapital,
                SeedKnobs.Baseline with
                {
                    FactoryCash = Money.From(25m),
                    OreUnitPrice = 8m,
                    WageCashReserve = 30m,
                    HaulTargetBuffer = 16m,
                    IncludeCreditFacility = true,
                    FacilityLimit = Money.From(80m),
                    OreOpeningAtFactory = 2m
                },
                (c, k) => new CompositePolicy(
                    new CreditDrawPolicy(
                        c,
                        Money.From(40m),
                        Money.From(k.WageCashReserve + 2m * k.OreUnitPrice)),
                    new OreHaulPolicy(c, k.OreUnitPrice, k.HaulTargetBuffer, k.HaulMaxPerPeriod, k.WageCashReserve))),

            ScenarioKind.CreditCycle => new(
                ScenarioKind.CreditCycle,
                SeedKnobs.Baseline with
                {
                    FactoryCash = Money.From(40m),
                    OreUnitPrice = 5m,
                    WageCashReserve = 20m,
                    IncludeCreditFacility = true,
                    FacilityLimit = Money.From(150m),
                    FactoryInstalled = 3m
                },
                (c, k) => new CompositePolicy(
                    new CreditDrawPolicy(
                        c,
                        Money.From(50m),
                        Money.From(k.WageCashReserve + 2m * k.OreUnitPrice)),
                    new CapacityExpandPolicy(c, afterPeriod: 40, newInstalledCapacity: 8m),
                    new OreHaulPolicy(c, k.OreUnitPrice, k.HaulTargetBuffer, k.HaulMaxPerPeriod, k.WageCashReserve))),

            ScenarioKind.FiscalStress => new(
                ScenarioKind.FiscalStress,
                SeedKnobs.Baseline with
                {
                    StateCash = Money.From(80m),
                    TransferPerHousehold = Money.From(3m),
                    HouseholdTaxRate = 0m,
                    FirmTaxRate = 0m,
                    HouseholdCash = Money.From(200m)
                },
                (c, k) => new OreHaulPolicy(c, k.OreUnitPrice, k.HaulTargetBuffer, k.HaulMaxPerPeriod, k.WageCashReserve)),

            ScenarioKind.Shock => new(
                ScenarioKind.Shock,
                SeedKnobs.Baseline with
                {
                    IncludeInsurance = true,
                    InsurancePremium = Money.From(5m),
                    InsuranceDeductible = Money.From(20m),
                    InsurerCash = Money.From(400m)
                },
                (c, k) => new CompositePolicy(
                    new ShockPolicy(c, Money.From(120m)),
                    new OreHaulPolicy(c, k.OreUnitPrice, k.HaulTargetBuffer, k.HaulMaxPerPeriod, k.WageCashReserve))),

            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
}
