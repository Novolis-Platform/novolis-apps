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
