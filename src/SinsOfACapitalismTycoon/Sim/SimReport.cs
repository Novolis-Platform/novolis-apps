using Novolis.Economy.Core;
using Novolis.Economy.Core.Extensions;

namespace SinsOfACapitalismTycoon.Sim;

internal sealed record DramaStats(
    int PeriodsWithoutProduction,
    int LongestProductionGap,
    decimal PeakMineOreStockpile,
    int FactoryOreStockoutPeriods,
    int DelinquentObligationSightings,
    int DefaultedObligationSightings,
    int CreditDraws,
    int ShocksInjected,
    int CapacityExpansions,
    decimal MoneyCreatedByPolicy);

/// <summary>Horizon-cumulative Core period flows (summed each Advance).</summary>
internal sealed record CumulativeFlowStats(
    decimal MoneyCreated,
    decimal MoneyDestroyed,
    decimal WagesAccrued,
    decimal TaxCollected,
    decimal TransfersPaid,
    decimal ObligationsPaid,
    decimal ProductionOutputValue,
    decimal LastPeriodNetMoneyCreated);

internal sealed record HorizonStats(
    decimal CumulativeProductionFlow,
    decimal PeakCash,
    decimal TroughCash,
    int PeriodsWithProduction,
    int TransfersStarted,
    decimal FinalOreAtMine,
    decimal FinalOreAtFactory,
    decimal FinalWidgets,
    TimeSpan Elapsed,
    DramaStats Drama,
    CumulativeFlowStats CumulativeFlows);

internal sealed record SimReport(
    ScenarioKind Scenario,
    ulong Seed,
    int RequestedPeriods,
    int FinalPeriod,
    int LogEvery,
    EconomySnapshot Snapshot,
    PeriodFlowInsight LastPeriodFlows,
    ObligationBookInsight Obligations,
    CreditBookInsight Credit,
    ProjectedAccountsSnapshot Accounts,
    IReadOnlyList<LegalEntityId> IlliquidButSolvent,
    IReadOnlyList<CohortInsight> Cohorts,
    IReadOnlyList<string> InvariantMessages,
    IReadOnlyList<EntityFinancialInsight> TopEntities,
    IReadOnlyList<RegionInsight> Regions,
    IReadOnlyList<string> PeriodLog,
    HorizonStats Horizon);
