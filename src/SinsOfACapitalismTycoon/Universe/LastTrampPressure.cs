using Novolis.Economy;
using Novolis.Economy.Accounting;
using Novolis.Economy.Simulation;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>
/// Last-tramp memoir pressure: rival light-commercial hulls are squeezed off the board
/// on a staggered schedule (cash skim + permanent suspension). Station premium advances
/// cannot revive a suspended rival — <see cref="ShipRegistryEntry.CanOperate"/> stays shut.
/// </summary>
internal static class LastTrampPressure
{
  /// <summary>First rival grounds around this campaign day (seed-stable order).</summary>
  public const int FirstSqueezeDay = 16;

  /// <summary>Days between each subsequent rival grounding.</summary>
  public const int SqueezeSpacingDays = 9;

  /// <summary>Extra daily skim on top of normal insurance (fraction of quoted premium).</summary>
  public const decimal RivalPremiumSkim = 0.85m;

  public static void TickDay(
    EconomySimulation sim,
    CampaignWorld.Ids ids,
    MilestoneLog milestones)
  {
    var world = sim.State.World;
    var day = sim.State.Clock.Date;
    var dayIndex = day.DayIndex;

    var rivals = ids.Registry.Entries
      .Where(TrampSurvival.IsLightCommercialTramp)
      .Where(e => !e.FirmId.Equals(ids.Carrier))
      .OrderBy(e => e.RegistryName, StringComparer.Ordinal)
      .ToList();

    for (var i = 0; i < rivals.Count; i++)
    {
      var entry = rivals[i];
      if (entry.Suspended || entry.BurnedOut || entry.Revoked)
      {
        continue;
      }

      if (!world.Ledgers.TryGetValue(entry.FirmId, out var firm)
          || !world.Ledgers.TryGetValue(ids.Registry.Underwriter, out var uw))
      {
        continue;
      }

      // Cash pressure while still sailing — makes standing fees bite before the squeeze day.
      var skim = Money.From(Math.Max(8m, ids.Registry.QuoteDailyPremium(entry) * RivalPremiumSkim));
      if (skim.Amount > 0.01m && firm.Cash.Amount + 0.0001m >= skim.Amount)
      {
        firm.Post(AccountRole.TransportTollExpense, AccountRole.Cash, skim, day, "Rival tramp market squeeze");
        uw.Post(AccountRole.Cash, AccountRole.Revenue, skim, day, "Rival tramp market squeeze");
        entry.PremiumPaid += skim.Amount;
      }
      else if (skim.Amount > 0.01m)
      {
        entry.PremiumArrearsDays++;
        if (entry.PremiumArrearsDays > 2)
        {
          entry.Insured = false;
        }
      }

      var dueDay = FirstSqueezeDay + i * SqueezeSpacingDays;
      if (dayIndex < dueDay)
      {
        continue;
      }

      entry.Suspended = true;
      entry.Insured = false;
      entry.PremiumArrearsDays = Math.Max(entry.PremiumArrearsDays, 3);
      milestones.AddOnce(dayIndex, "last-tramp-squeeze",
        $"market squeeze grounded {entry.RegistryName}");
    }
  }
}
