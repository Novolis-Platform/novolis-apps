using Novolis.Economy.Core;
using Novolis.Economy.Core.Extensions;

namespace SinsOfACapitalismTycoon.Sim;

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
