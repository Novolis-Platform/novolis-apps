using Novolis.Economy;
using Novolis.Economy.Accounting;
using Novolis.Economy.Logistics;
using Novolis.Economy.Simulation;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>
/// Debt follows the hull: uninsured + lien → suspend until lien service paid to Station.
/// </summary>
internal static class LienPulse
{
  public static void TickDay(
    EconomySimulation sim,
    CampaignWorld.Ids ids,
    MilestoneLog milestones)
  {
    var world = sim.State.World;
    var day = sim.State.Clock.Date.DayIndex;
    var date = sim.State.Clock.Date;
    if (!world.Ledgers.TryGetValue(ids.Station, out var station))
    {
      return;
    }

    foreach (var entry in ids.Registry.Entries.OrderBy(e => e.FirmId.Value))
    {
      if (entry.LienPrincipal <= 0m)
      {
        continue;
      }

      if (!world.Ledgers.TryGetValue(entry.FirmId, out var firm))
      {
        continue;
      }

      // Service 8% of lien or 80 CR floor when cash allows.
      var due = Money.From(Math.Max(80m, entry.LienPrincipal * 0.08m));
      if (firm.Cash.Amount + 0.0001m >= due.Amount)
      {
        firm.Post(AccountRole.TransportTollExpense, AccountRole.Cash, due, date, "Hull lien service");
        station.Post(AccountRole.Cash, AccountRole.Revenue, due, date, "Hull lien service");
        entry.LienPrincipal = Math.Max(0m, entry.LienPrincipal - due.Amount);
        if (entry.LienPrincipal <= 1m)
        {
          entry.LienPrincipal = 0m;
          if (entry.Suspended && entry.Insured && !entry.BurnedOut)
          {
            entry.Suspended = false;
          }

          milestones.Add(day, "lien", $"{entry.RegistryName} cleared");
        }

        continue;
      }

      // Uninsured past grace with outstanding lien → suspend (debt follows hull).
      if (!entry.Insured && entry.PremiumArrearsDays > FtlDriveLifePolicy.PremiumGraceDays)
      {
        entry.Suspended = true;
        entry.YardCleared = false;
        milestones.AddOnce(day, "lien",
          $"{entry.RegistryName} hold — lien {entry.LienPrincipal:0}");
      }
    }
  }
}
