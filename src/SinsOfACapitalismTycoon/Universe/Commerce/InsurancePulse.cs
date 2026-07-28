using Novolis.Economy;
using Novolis.Economy.Accounting;
using Novolis.Economy.Logistics;
using Novolis.Economy.Simulation;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>
/// Registry insurance: premiums track drive-life risk and <b>operating days</b>.
/// Idle hulls pay a cheap standing fee (or nothing when uninsured) so a short cash dip
/// does not permanently ground the fleet. Maintenance/overhaul stays separate.
/// </summary>
internal static class InsurancePulse
{
  public static void TickDay(
    EconomySimulation sim,
    ShipRegistry registry,
    MilestoneLog milestones,
    CreditCirculation? credits = null) =>
    TickDayCore(sim, registry, milestones, credits, chargePremiums: true);

  /// <summary>Morning pass: reinstate / standing only so agents can sail before evening billing.</summary>
  public static void TickMorningReinstate(
    EconomySimulation sim,
    ShipRegistry registry,
    MilestoneLog milestones) =>
    TickDayCore(sim, registry, milestones, null, chargePremiums: false);

  private static void TickDayCore(
    EconomySimulation sim,
    ShipRegistry registry,
    MilestoneLog milestones,
    CreditCirculation? credits,
    bool chargePremiums)
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

      var operating = world.Shipments.Any(s =>
        s.FirmId.Equals(entry.FirmId)
        && !s.IsLegacy
        && s.Status == ShipmentStatus.InTransit);

      if (entry.BurnedOut || entry.Suspended)
      {
        grounded++;
        milestones.AddOnce(dayIndex, "grounding",
          entry.BurnedOut
            ? $"burned-out {entry.RegistryName}"
            : $"suspended {entry.RegistryName}");
        if (chargePremiums)
        {
          TryStandingFee(firmLedger, underwriterLedger, entry, registry, day);
        }

        continue;
      }

      if (!entry.Insured)
      {
        var reinstateQuote = Money.From(registry.QuoteDailyPremium(entry) * (operating ? 1m : 0.15m));
        if (firmLedger.Cash.Amount + 0.0001m >= reinstateQuote.Amount && reinstateQuote.Amount > 0m)
        {
          if (chargePremiums)
          {
            firmLedger.Post(
              AccountRole.TransportTollExpense, AccountRole.Cash, reinstateQuote, day, "Hull insurance premium");
            underwriterLedger.Post(
              AccountRole.Cash, AccountRole.Revenue, reinstateQuote, day, "Hull insurance premium");
            entry.PremiumPaid += reinstateQuote.Amount;
          }

          entry.Insured = true;
          entry.PremiumArrearsDays = 0;
          milestones.AddOnce(dayIndex, "reinstated", $"{entry.RegistryName} cash");
          continue;
        }

        if (underwriterLedger.Cash.Amount > 25_000m
            && TryStationAdvance(firmLedger, underwriterLedger, reinstateQuote.Amount > 0m ? reinstateQuote : Money.From(14m), day, entry, milestones, dayIndex))
        {
          entry.PremiumArrearsDays = 0;
          continue;
        }

        grounded++;
        milestones.AddOnce(dayIndex, "grounding", $"uninsured {entry.RegistryName}");
        continue;
      }

      if (!chargePremiums)
      {
        continue;
      }

      var premium = Money.From(registry.QuoteDailyPremium(entry));
      if (!operating)
      {
        premium = Money.From(Math.Round(premium.Amount * 0.15m, 2, MidpointRounding.AwayFromZero));
      }

      if (premium.Amount <= 0m)
      {
        continue;
      }

      if (firmLedger.Cash.Amount + 0.0001m < premium.Amount)
      {
        entry.PremiumArrearsDays++;
        if (entry.PremiumArrearsDays > FtlDriveLifePolicy.PremiumGraceDays)
        {
          entry.Insured = false;
          grounded++;
          milestones.AddOnce(dayIndex, "grounding", $"uninsured {entry.RegistryName}");
        }
        else
        {
          milestones.AddOnce(dayIndex, "arrears",
            $"{entry.RegistryName} d{entry.PremiumArrearsDays}");
        }

        continue;
      }

      firmLedger.Post(
        AccountRole.TransportTollExpense, AccountRole.Cash, premium, day,
        operating ? "Hull insurance premium" : "Hull idle standing");
      underwriterLedger.Post(
        AccountRole.Cash, AccountRole.Revenue, premium, day,
        operating ? "Hull insurance premium" : "Hull idle standing");
      entry.PremiumPaid += premium.Amount;
      entry.PremiumArrearsDays = 0;
      entry.Insured = true;
    }

    if (grounded >= 2)
    {
      milestones.AddOnce(dayIndex, "grounding", $"cascade grounded≥{grounded}");
    }
  }

  private static void TryStandingFee(
    FirmLedger firm,
    FirmLedger underwriter,
    ShipRegistryEntry entry,
    ShipRegistry registry,
    SimulationDate day)
  {
    var stand = Money.From(registry.QuoteDailyPremium(entry));
    if (stand.Amount <= 0m || firm.Cash.Amount + 0.0001m < stand.Amount)
    {
      return;
    }

    firm.Post(AccountRole.TransportTollExpense, AccountRole.Cash, stand, day, "Hull standing fee");
    underwriter.Post(AccountRole.Cash, AccountRole.Revenue, stand, day, "Hull standing fee");
    entry.PremiumPaid += stand.Amount;
  }

  private static bool TryStationAdvance(
    FirmLedger firm,
    FirmLedger station,
    Money premium,
    SimulationDate day,
    ShipRegistryEntry entry,
    MilestoneLog milestones,
    int dayIndex)
  {
    if (station.Cash.Amount + 0.0001m < premium.Amount * 2m)
    {
      return false;
    }

    // Station advances premium (expense) → firm cash; firm immediately remits to UW (same firm if Station=UW).
    // When Station is underwriter, net is accounting noise; still restores Insured standing.
    station.Post(AccountRole.WageExpense, AccountRole.Cash, premium, day, "Premium advance");
    firm.Post(AccountRole.Cash, AccountRole.Revenue, premium, day, "Premium advance");
    firm.Post(AccountRole.TransportTollExpense, AccountRole.Cash, premium, day, "Hull insurance premium");
    station.Post(AccountRole.Cash, AccountRole.Revenue, premium, day, "Hull insurance premium");
    entry.PremiumPaid += premium.Amount;
    entry.Insured = true;
    entry.PremiumArrearsDays = 0;
    milestones.AddOnce(dayIndex, "reinstated", $"{entry.RegistryName} premium advance");
    return true;
  }
}
