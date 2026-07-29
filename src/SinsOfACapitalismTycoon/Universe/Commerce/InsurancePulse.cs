using Novolis.Economy;
using Novolis.Economy.Logistics;
using Novolis.Economy.Simulation;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>
/// Registry insurance as a <b>category over time</b>: daily accrual like wages
/// (expense → payable), then cash settlement when affordable — not per-job cash hits.
/// </summary>
internal static class InsurancePulse
{
  public static void TickDay(
    EconomySimulation sim,
    ShipRegistry registry,
    MilestoneLog milestones,
    CreditCirculation? credits = null) =>
    TickDayCore(sim, registry, milestones, credits, settlePass: true);

  /// <summary>Morning: try settle payable so agents can sail; accrual happens on the day pass.</summary>
  public static void TickMorningReinstate(
    EconomySimulation sim,
    ShipRegistry registry,
    MilestoneLog milestones) =>
    TickDayCore(sim, registry, milestones, null, settlePass: false);

  public static bool IsOperating(EconomyWorld world, FirmId firm) =>
    world.Shipments.Any(s =>
      s.FirmId.Equals(firm)
      && !s.IsLegacy
      && s.Status == ShipmentStatus.InTransit);

  private static void TickDayCore(
    EconomySimulation sim,
    ShipRegistry registry,
    MilestoneLog milestones,
    CreditCirculation? credits,
    bool settlePass)
  {
    _ = credits;
    var world = sim.State.World;
    var day = sim.State.Clock.Date;
    var dayIndex = day.DayIndex;
    var grounded = 0;

    foreach (var entry in registry.Entries.OrderBy(e => e.FirmId.Value))
    {
      if (!world.Ledgers.TryGetValue(entry.FirmId, out var firmLedger)
          || !world.Ledgers.TryGetValue(registry.Underwriter, out var underwriterLedger))
      {
        continue;
      }

      var daily = Money.From(registry.QuotePremiumDue(entry));

      if (entry.BurnedOut || entry.Suspended)
      {
        grounded++;
        milestones.AddOnce(dayIndex, "grounding",
          entry.BurnedOut
            ? $"burned-out {entry.RegistryName}"
            : $"suspended {entry.RegistryName}");
        if (settlePass && daily.Amount > 0m)
        {
          HullFinance.AccruePremium(firmLedger, entry, daily, day, HullFinance.IdleStandingMemo);
          HullFinance.TrySettlePremium(firmLedger, underwriterLedger, entry, day);
        }

        continue;
      }

      if (settlePass && entry.Insured && daily.Amount > 0m)
      {
        // Operating (underway): full category rate. Docked idle: standing fee.
        var operating = IsOperating(world, entry.FirmId);
        var accrue = operating
          ? daily
          : Money.From(daily.Amount * CampaignWorld.IdleStandingPremiumFactor);
        HullFinance.AccruePremium(
          firmLedger,
          entry,
          accrue,
          day,
          operating ? HullFinance.PremiumAccrualMemo : HullFinance.IdleStandingMemo);
      }

      var settled = HullFinance.TrySettlePremium(firmLedger, underwriterLedger, entry, day);

      if (entry.PremiumPayable > 0.0001m && !settled)
      {
        entry.PremiumArrearsDays++;
      }
      else if (settled && entry.PremiumPayable <= 0.0001m)
      {
        entry.PremiumArrearsDays = 0;
      }

      var payableCap = daily.Amount > 0m
        ? daily.Amount * FtlDriveLifePolicy.PremiumGraceDays
        : 0m;

      if (!entry.Insured)
      {
        if (entry.PremiumPayable <= 0.0001m)
        {
          entry.Insured = true;
          entry.PremiumArrearsDays = 0;
          entry.Suspended = false;
          milestones.AddOnce(dayIndex, "reinstated", $"{entry.RegistryName} settled");
          continue;
        }

        grounded++;
        milestones.AddOnce(dayIndex, "grounding", $"uninsured {entry.RegistryName}");
        continue;
      }

      // Still marked insured: drop cover if payable snowballs past grace window.
      if (payableCap > 0m && entry.PremiumPayable > payableCap + 0.0001m)
      {
        entry.Insured = false;
        grounded++;
        milestones.AddOnce(dayIndex, "grounding", $"uninsured {entry.RegistryName}");
        continue;
      }

      if (entry.PremiumArrearsDays > FtlDriveLifePolicy.PremiumGraceDays
          && entry.PremiumPayable > 0.0001m)
      {
        entry.Insured = false;
        grounded++;
        milestones.AddOnce(dayIndex, "grounding", $"uninsured {entry.RegistryName}");
        continue;
      }

      if (entry.PremiumArrearsDays > 0 && entry.PremiumPayable > 0.0001m)
      {
        milestones.AddOnce(dayIndex, "arrears",
          $"{entry.RegistryName} d{entry.PremiumArrearsDays} payable {entry.PremiumPayable:0.#}");
      }
    }

    if (grounded >= 2)
    {
      milestones.AddOnce(dayIndex, "grounding", $"cascade grounded≥{grounded}");
    }
  }
}
