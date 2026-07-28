using System.Globalization;
using System.Text;
using Novolis.Economy.Core;
using Novolis.Economy.Core.Extensions;

namespace SinsOfACapitalismTycoon.Sim;

internal static class ReportFormatter
{
    public static string Format(SimReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Sins of a Capitalism Tycoon");
        sb.AppendLine(new string('=', 40));
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"Scenario {ScenarioKindParser.ToArg(report.Scenario)}  |  seed {report.Seed}  |  " +
            $"periods {report.RequestedPeriods} → t={report.FinalPeriod}  |  log every {report.LogEvery}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"Elapsed {report.Horizon.Elapsed.TotalSeconds:0.###}s");
        sb.AppendLine();

        var h = report.Horizon;
        var d = h.Drama;
        sb.AppendLine("Drama");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"  periods without factory production {d.PeriodsWithoutProduction}/{report.RequestedPeriods}  " +
            $"(longest gap {d.LongestProductionGap})");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"  peak mine ore stockpile {d.PeakMineOreStockpile:0.#}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"  factory ore stockout periods {d.FactoryOreStockoutPeriods}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"  delinquent/defaulted obligation sightings {d.DelinquentObligationSightings}/{d.DefaultedObligationSightings}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"  credit draws {d.CreditDraws}  money created by policy {d.MoneyCreatedByPolicy:0.####}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"  shocks injected {d.ShocksInjected}  capacity expansions {d.CapacityExpansions}");
        sb.AppendLine();

        sb.AppendLine("Horizon");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"  cumulative production flow {h.CumulativeProductionFlow:0.####}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"  periods with production {h.PeriodsWithProduction}/{report.RequestedPeriods}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"  cash peak/trough {h.PeakCash:0.####} / {h.TroughCash:0.####}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"  ore hauls started {h.TransfersStarted}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"  final ore mine/factory {h.FinalOreAtMine:0.#} / {h.FinalOreAtFactory:0.#}  widgets {h.FinalWidgets:0.#}");
        sb.AppendLine();

        var cf = h.CumulativeFlows;
        sb.AppendLine("Flows");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"  cum created/destroyed/net {cf.MoneyCreated:0.####}/{cf.MoneyDestroyed:0.####}/{(cf.MoneyCreated - cf.MoneyDestroyed):0.####}  " +
            $"(last-period net {cf.LastPeriodNetMoneyCreated:0.####})");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"  cum wages/tax/transfers {cf.WagesAccrued:0.####}/{cf.TaxCollected:0.####}/{cf.TransfersPaid:0.####}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"  cum obligations paid {cf.ObligationsPaid:0.####}");
        sb.AppendLine();

        var credit = report.Credit;
        sb.AppendLine("Credit");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"  loans performing/delinq/default {credit.PerformingLoans}/{credit.DelinquentLoans}/{credit.DefaultedLoans}  " +
            $"principal {Fmt(credit.LoanPrincipalOutstanding)}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"  facilities {credit.FacilityCount}  drawn/limit {Fmt(credit.FacilityDrawnTotal)}/{Fmt(credit.FacilityLimitTotal)}  " +
            $"undrawn {Fmt(credit.UndrawnCommitted)}");
        sb.AppendLine();

        var ob = report.Obligations;
        sb.AppendLine("Obligations");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"  pending/delinq/defaulted {ob.PendingCount}/{ob.DelinquentCount}/{ob.DefaultedCount}  due now {Fmt(ob.DueNow)}");
        if (ob.PendingSumByKind.Count > 0)
        {
            var top = ob.PendingSumByKind.OrderByDescending(kv => kv.Value.Amount).First();
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"  top pending kind {top.Key} {Fmt(top.Value)}");
        }
        sb.AppendLine();

        sb.AppendLine("Stress");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"  illiquid-but-solvent {report.IlliquidButSolvent.Count}");
        foreach (var id in report.IlliquidButSolvent.Take(3))
            sb.AppendLine(CultureInfo.InvariantCulture, $"    {Short(id)}");
        sb.AppendLine();

        sb.AppendLine("Cohorts");
        foreach (var c in report.Cohorts)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"  {Short(c.Id)}  hh {c.HouseholdCount}  cash {Fmt(c.TotalCash)}  labor h {c.EffectiveLaborHours:0.#}");
        }
        sb.AppendLine();

        sb.AppendLine("Invariants");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  violations {report.InvariantMessages.Count}");
        foreach (var msg in report.InvariantMessages.Take(3))
            sb.AppendLine($"    {msg}");
        sb.AppendLine();

        var accounts = report.Accounts;
        sb.AppendLine("Accounts (projected from Core)");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"  aggregate net worth {Fmt(accounts.AggregateNetWorth)}  " +
            $"unpriced holdings qty {accounts.AggregateHoldingsUnpricedQuantity:0.#}");
        var pl = accounts.LastPeriod;
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"  last-period wages/tax/transfers {Fmt(pl.WagesAccrued)}/{Fmt(pl.TaxCollected)}/{Fmt(pl.TransfersPaid)}  " +
            $"prod-flow {Fmt(pl.ProductionOutputValue)}");
        foreach (var sec in accounts.Sectors)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"  {sec.Kind} n={sec.EntityCount}  cash {Fmt(sec.Cash)}  dep {Fmt(sec.DepositsHeld)}  " +
                $"loans A/L {Fmt(sec.LoansReceivable)}/{Fmt(sec.LoansPayable)}  NW {Fmt(sec.NetWorth)}");
        }

        foreach (var e in accounts.Entities
                     .OrderByDescending(x => Math.Abs(x.NetWorth.Amount))
                     .Take(4))
        {
            var undrawn = e.UndrawnCommittedCredit.Amount > 0m
                ? $"  undrawn {Fmt(e.UndrawnCommittedCredit)}"
                : "";
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"    {e.Kind} {Short(e.Id)}  A {Fmt(e.TotalAssets)}  L {Fmt(e.TotalLiabilities)}  " +
                $"NW {Fmt(e.NetWorth)}  hold$ {Fmt(e.HoldingsValued)} unpriced {e.HoldingsUnpricedQuantity:0.#}{undrawn}");
        }
        sb.AppendLine();

        var s = report.Snapshot;
        sb.AppendLine("Snapshot");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"  entities {s.EntityCount}  regions {s.RegionCount}  cohorts {s.CohortCount}  households {s.HouseholdCount}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"  activities {s.ActivityCount}  holdings {s.HoldingSlots}  transfers in-flight {s.InFlightTransfers}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"  cash {Fmt(s.TotalCash)}  deposits {Fmt(s.TotalDeposits)}  broad money {Fmt(s.BroadMoney)}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"  loans performing/delinq/default {s.PerformingLoans}/{s.DelinquentLoans}/{s.DefaultedLoans}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"  obligations pending/delinq {s.PendingObligations}/{s.DelinquentObligations}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"  loan principal {Fmt(s.LoanPrincipalOutstanding)}  undrawn committed {Fmt(s.UndrawnCommittedCredit)}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"  net money created (last period) {Fmt(s.NetMoneyCreatedThisPeriod)}");

        if (s.CashByKind.Count > 0)
        {
            sb.AppendLine("  cash by kind:");
            foreach (var (kind, cash) in s.CashByKind.OrderBy(kv => kv.Key.ToString()))
                sb.AppendLine(CultureInfo.InvariantCulture, $"    {kind}: {Fmt(cash)}");
        }

        sb.AppendLine();
        sb.AppendLine("Regions");
        foreach (var r in report.Regions)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"  {Short(r.Id)}  hh {r.Households}/{r.LivingCapacity} (util {r.LivingUtilization:0.##})  " +
                $"prod util {r.ProductionUtilization:0.##}  labor h {r.LaborSupplyHours:0.#}  activities {r.ActivityCount}");
        }

        sb.AppendLine();
        sb.AppendLine("Entities");
        foreach (var e in report.TopEntities)
        {
            var flags = (e.IsIlliquid ? " illiquid" : "") + (e.IsInsolventHint ? " insolvent?" : "");
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"  {e.Kind} {Short(e.Id)}  cash {Fmt(e.Cash)}  deposits {Fmt(e.Deposits)}  " +
                $"solvency {Fmt(e.SimpleSolvency)}  due {Fmt(e.PendingObligationsDue)}{flags}");
        }

        if (report.PeriodLog.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"Period log (sampled every {report.LogEvery})");
            foreach (var line in report.PeriodLog)
                sb.AppendLine($"  {line}");
        }

        return sb.ToString();
    }

    private static string Fmt(Money m) => m.Amount.ToString("0.####", CultureInfo.InvariantCulture);
    private static string Short(LegalEntityId id) => id.Value.ToString("N")[..8];
    private static string Short(RegionId id) => id.Value.ToString("N")[..8];
    private static string Short(CohortId id) => id.Value.ToString("N")[..8];
}
