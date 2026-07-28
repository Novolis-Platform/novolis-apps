using System.Diagnostics;
using Novolis.Economy.Core;
using Novolis.Economy.Core.Extensions;
using Novolis.Economy.Core.Holdings;
using Novolis.Economy.Core.Steps;

namespace SinsOfACapitalismTycoon.Sim;

internal static class GameRunner
{
    public static SimReport Run(
        ScenarioKind scenario,
        ulong seed,
        int periods,
        int logEvery = 0,
        Action<int, int>? progress = null)
    {
        if (periods < 1)
            throw new ArgumentOutOfRangeException(nameof(periods));

        var sampleEvery = logEvery > 0
            ? logEvery
            : periods <= 50 ? 1 : Math.Max(1, periods / 40);

        var pack = ScenarioCatalog.Get(scenario);
        var counters = new Policies.PolicyCounters();
        var policy = pack.CreatePolicy(counters, pack.Knobs);
        var (state, ids, knobs) = SeedEconomy.Create(seed, pack.Knobs);

        var engine = DefaultPeriodPipeline.CreateEngine();
        var sw = Stopwatch.StartNew();

        decimal cumProd = 0m;
        decimal cumMoneyCreated = 0m;
        decimal cumMoneyDestroyed = 0m;
        decimal cumWages = 0m;
        decimal cumTax = 0m;
        decimal cumTransfers = 0m;
        decimal cumObligationsPaid = 0m;
        decimal peakCash = state.TotalCash().Amount;
        decimal troughCash = peakCash;
        var periodsWithProd = 0;
        var periodsWithoutProd = 0;
        var currentGap = 0;
        var longestGap = 0;
        decimal peakMineOre = HoldingLedger.GetQuantity(state, ids.MinerFirm, ids.MineRegion, ids.OreId);
        var factoryStockouts = 0;
        var delinquentSightings = 0;
        var defaultedSightings = 0;

        var log = new List<string>
        {
            FormatLogLine(0, state, counters.TransfersStarted)
        };

        for (var i = 0; i < periods; i++)
        {
            state = policy.ApplyIntents(state, ids, i, periods);
            state = engine.Advance(state);

            var cash = state.TotalCash().Amount;
            if (cash > peakCash) peakCash = cash;
            if (cash < troughCash) troughCash = cash;

            var flows = state.Flows;
            cumProd += flows.ProductionOutputValue.Amount;
            cumMoneyCreated += flows.MoneyCreated.Amount;
            cumMoneyDestroyed += flows.MoneyDestroyed.Amount;
            cumWages += flows.WagesAccrued.Amount;
            cumTax += flows.TaxCollected.Amount;
            cumTransfers += flows.TransfersPaid.Amount;
            cumObligationsPaid += flows.ObligationsPaid.Amount;

            var factoryRuns = state.Scratch.ActualRuns.TryGetValue(ids.FactoryActivityId, out var fr) ? fr : 0m;
            if (factoryRuns > 0m)
            {
                periodsWithProd++;
                currentGap = 0;
            }
            else
            {
                periodsWithoutProd++;
                currentGap++;
                if (currentGap > longestGap)
                    longestGap = currentGap;
            }

            var mineOre = HoldingLedger.GetQuantity(state, ids.MinerFirm, ids.MineRegion, ids.OreId);
            if (mineOre > peakMineOre)
                peakMineOre = mineOre;

            var factoryOre = HoldingLedger.GetQuantity(state, ids.FactoryFirm, ids.FactoryRegion, ids.OreId);
            if (factoryOre < 2m)
                factoryStockouts++;

            delinquentSightings += state.Obligations.Count(o => o.Status == ObligationStatus.Delinquent);
            defaultedSightings += state.Obligations.Count(o => o.Status == ObligationStatus.Defaulted);

            var t = state.Period;
            if (t == periods || t % sampleEvery == 0)
                log.Add(FormatLogLine(t, state, counters.TransfersStarted));

            progress?.Invoke(i + 1, periods);
        }

        sw.Stop();

        var oreMine = HoldingLedger.GetQuantity(state, ids.MinerFirm, ids.MineRegion, ids.OreId);
        var oreFactory = HoldingLedger.GetQuantity(state, ids.FactoryFirm, ids.FactoryRegion, ids.OreId);
        var widgets =
            HoldingLedger.GetQuantity(state, ids.FactoryFirm, ids.FactoryRegion, ids.WidgetId)
            + HoldingLedger.GetQuantity(state, ids.HouseholdId, ids.FactoryRegion, ids.WidgetId);

        var invariants = state.CheckInvariants();

        return new SimReport(
            Scenario: scenario,
            Seed: seed,
            RequestedPeriods: periods,
            FinalPeriod: state.Period,
            LogEvery: sampleEvery,
            Snapshot: state.Snapshot(),
            LastPeriodFlows: state.FlowInsight(),
            Obligations: state.ObligationBook(),
            Credit: state.CreditBook(),
            Accounts: state.ProjectedAccounts(),
            IlliquidButSolvent: state.IlliquidButSolventEntities(),
            Cohorts: state.CohortInsights(),
            InvariantMessages: invariants.Select(v => $"{v.Code}: {v.Message}").ToList(),
            TopEntities: state.EntityInsights().OrderBy(e => e.Kind.ToString()).ThenBy(e => e.Id.Value).ToList(),
            Regions: state.RegionInsights().ToList(),
            PeriodLog: log,
            Horizon: new HorizonStats(
                CumulativeProductionFlow: cumProd,
                PeakCash: peakCash,
                TroughCash: troughCash,
                PeriodsWithProduction: periodsWithProd,
                TransfersStarted: counters.TransfersStarted,
                FinalOreAtMine: oreMine,
                FinalOreAtFactory: oreFactory,
                FinalWidgets: widgets,
                Elapsed: sw.Elapsed,
                Drama: new DramaStats(
                    PeriodsWithoutProduction: periodsWithoutProd,
                    LongestProductionGap: longestGap,
                    PeakMineOreStockpile: peakMineOre,
                    FactoryOreStockoutPeriods: factoryStockouts,
                    DelinquentObligationSightings: delinquentSightings,
                    DefaultedObligationSightings: defaultedSightings,
                    CreditDraws: counters.CreditDraws,
                    ShocksInjected: counters.ShocksInjected,
                    CapacityExpansions: counters.CapacityExpansions,
                    MoneyCreatedByPolicy: counters.MoneyCreatedByPolicy),
                CumulativeFlows: new CumulativeFlowStats(
                    MoneyCreated: cumMoneyCreated,
                    MoneyDestroyed: cumMoneyDestroyed,
                    WagesAccrued: cumWages,
                    TaxCollected: cumTax,
                    TransfersPaid: cumTransfers,
                    ObligationsPaid: cumObligationsPaid,
                    ProductionOutputValue: cumProd,
                    LastPeriodNetMoneyCreated: state.Flows.NetMoneyCreated.Amount)));
    }

    private static string FormatLogLine(int period, EconomyState state, int transfersStarted) =>
        $"t={period} cash={state.TotalCash()} deposits={state.TotalDeposits()} " +
        $"prod={state.Flows.ProductionOutputValue} xfers_started_cum={transfersStarted} " +
        $"in_flight={state.Transfers.Count}";
}
