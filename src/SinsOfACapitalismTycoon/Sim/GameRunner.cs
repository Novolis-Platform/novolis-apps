using System.Diagnostics;
using Novolis.Economy.Core;
using Novolis.Economy.Core.Extensions;
using Novolis.Economy.Core.Holdings;
using Novolis.Economy.Simulation.Bounded;

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

        var engine = DefaultBoundedPeriodPipeline.CreateEngine();
        var bounded = new BoundedPeriodState(state);
        var sw = Stopwatch.StartNew();
        var tallies = new PeriodTallies(state, ids);
        var log = new List<string> { FormatLogLine(0, state, counters.TransfersStarted) };

        for (var i = 0; i < periods; i++)
        {
            bounded = bounded.WithEconomy(policy.ApplyIntents(bounded.Economy, ids, i, periods));
            bounded = engine.Advance(bounded);
            state = bounded.Economy;
            tallies.Accumulate(state, bounded, ids);
            MaybeSampleLog(log, state, counters.TransfersStarted, periods, sampleEvery);
            progress?.Invoke(i + 1, periods);
        }

        sw.Stop();
        return BuildReport(scenario, seed, periods, sampleEvery, state, ids, counters, tallies, log, sw.Elapsed);
    }

    private sealed class PeriodTallies
    {
        public decimal CumProd;
        public decimal CumMoneyCreated;
        public decimal CumMoneyDestroyed;
        public decimal CumWages;
        public decimal CumTax;
        public decimal CumTransfers;
        public decimal CumObligationsPaid;
        public decimal PeakCash;
        public decimal TroughCash;
        public int PeriodsWithProd;
        public int PeriodsWithoutProd;
        public int CurrentGap;
        public int LongestGap;
        public decimal PeakMineOre;
        public int FactoryStockouts;
        public int DelinquentSightings;
        public int DefaultedSightings;

        public PeriodTallies(EconomyState state, SeedIds ids)
        {
            PeakCash = state.TotalCash().Amount;
            TroughCash = PeakCash;
            PeakMineOre = HoldingLedger.GetQuantity(state, ids.MinerFirm, ids.MineRegion, ids.OreId);
        }

        public void Accumulate(EconomyState state, BoundedPeriodState bounded, SeedIds ids)
        {
            var cash = state.TotalCash().Amount;
            if (cash > PeakCash) PeakCash = cash;
            if (cash < TroughCash) TroughCash = cash;

            var flows = state.Flows;
            CumProd += flows.ProductionOutputValue.Amount;
            CumMoneyCreated += flows.MoneyCreated.Amount;
            CumMoneyDestroyed += flows.MoneyDestroyed.Amount;
            CumWages += flows.WagesAccrued.Amount;
            CumTax += flows.TaxCollected.Amount;
            CumTransfers += flows.TransfersPaid.Amount;
            CumObligationsPaid += flows.ObligationsPaid.Amount;

            var factoryRuns = bounded.Scratch.ActualRuns.TryGetValue(ids.FactoryActivityId, out var fr) ? fr : 0m;
            if (factoryRuns > 0m)
            {
                PeriodsWithProd++;
                CurrentGap = 0;
            }
            else
            {
                PeriodsWithoutProd++;
                CurrentGap++;
                if (CurrentGap > LongestGap)
                    LongestGap = CurrentGap;
            }

            var mineOre = HoldingLedger.GetQuantity(state, ids.MinerFirm, ids.MineRegion, ids.OreId);
            if (mineOre > PeakMineOre)
                PeakMineOre = mineOre;

            var factoryOre = HoldingLedger.GetQuantity(state, ids.FactoryFirm, ids.FactoryRegion, ids.OreId);
            if (factoryOre < 2m)
                FactoryStockouts++;

            DelinquentSightings += state.Obligations.Count(o => o.Status == ObligationStatus.Delinquent);
            DefaultedSightings += state.Obligations.Count(o => o.Status == ObligationStatus.Defaulted);
        }
    }

    private static void MaybeSampleLog(
        List<string> log,
        EconomyState state,
        int transfersStarted,
        int periods,
        int sampleEvery)
    {
        var t = state.Period;
        if (t == periods || t % sampleEvery == 0)
            log.Add(FormatLogLine(t, state, transfersStarted));
    }

    private static SimReport BuildReport(
        ScenarioKind scenario,
        ulong seed,
        int periods,
        int sampleEvery,
        EconomyState state,
        SeedIds ids,
        Policies.PolicyCounters counters,
        PeriodTallies tallies,
        List<string> log,
        TimeSpan elapsed)
    {
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
                CumulativeProductionFlow: tallies.CumProd,
                PeakCash: tallies.PeakCash,
                TroughCash: tallies.TroughCash,
                PeriodsWithProduction: tallies.PeriodsWithProd,
                TransfersStarted: counters.TransfersStarted,
                FinalOreAtMine: oreMine,
                FinalOreAtFactory: oreFactory,
                FinalWidgets: widgets,
                Elapsed: elapsed,
                Drama: new DramaStats(
                    PeriodsWithoutProduction: tallies.PeriodsWithoutProd,
                    LongestProductionGap: tallies.LongestGap,
                    PeakMineOreStockpile: tallies.PeakMineOre,
                    FactoryOreStockoutPeriods: tallies.FactoryStockouts,
                    DelinquentObligationSightings: tallies.DelinquentSightings,
                    DefaultedObligationSightings: tallies.DefaultedSightings,
                    CreditDraws: counters.CreditDraws,
                    ShocksInjected: counters.ShocksInjected,
                    CapacityExpansions: counters.CapacityExpansions,
                    MoneyCreatedByPolicy: counters.MoneyCreatedByPolicy),
                CumulativeFlows: new CumulativeFlowStats(
                    MoneyCreated: tallies.CumMoneyCreated,
                    MoneyDestroyed: tallies.CumMoneyDestroyed,
                    WagesAccrued: tallies.CumWages,
                    TaxCollected: tallies.CumTax,
                    TransfersPaid: tallies.CumTransfers,
                    ObligationsPaid: tallies.CumObligationsPaid,
                    ProductionOutputValue: tallies.CumProd,
                    LastPeriodNetMoneyCreated: state.Flows.NetMoneyCreated.Amount)));
    }

    private static string FormatLogLine(int period, EconomyState state, int transfersStarted) =>
        $"t={period} cash={state.TotalCash()} deposits={state.TotalDeposits()} " +
        $"prod={state.Flows.ProductionOutputValue} xfers_started_cum={transfersStarted} " +
        $"in_flight={state.Transfers.Count}";
}
