using Novolis.Economy;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>
/// Ranked next-act hint for the captain desk — why standing hurts and what to do now.
/// Does not change economy rules; copy only.
/// </summary>
internal static class CaptainCoach
{
  public const string Prefix = "NEXT:";

  public sealed record Advice(string CoachLine, string SoftFailEnrichment);

  public static Advice For(CampaignRunner.LiveSession session)
  {
    var sim = session.Sim;
    var ids = session.Ids;
    var player = session.Player;
    var entry = ids.Registry.TryGet(ids.Carrier);
    var cash = sim.State.World.Ledgers.TryGetValue(ids.Carrier, out var led)
      ? led.Cash.Amount
      : 0m;
    var listPremium = entry is null ? 0m : ids.Registry.QuotePremiumDue(entry);
    var operating = InsurancePulse.IsOperating(sim.State.World, ids.Carrier);
    var premium = operating
      ? listPremium
      : listPremium * CampaignWorld.IdleStandingPremiumFactor;
    var payable = entry?.PremiumPayable ?? 0m;
    var canOperate = entry?.CanOperate == true;
    var cause = GroundingCause(entry);
    var recovery = RecoveryCta(entry, cash, premium, payable, ids, operating);

    var soft = "";
    if (player.SoftFailRaised || player.SoftFailGroundedDays > 0)
    {
      var head = player.SoftFailRaised
        ? $"SOFT FAIL — grounded {player.SoftFailGroundedDays}d"
        : $"grounded streak {player.SoftFailGroundedDays}d";
      soft = string.IsNullOrEmpty(cause)
        ? $"{head} · {recovery}"
        : $"{head} · {cause} · {recovery}";
    }

    if (!canOperate)
    {
      return new Advice($"{Prefix} {cause} — {recovery}", soft);
    }

    if (player.Opportunities?.ActiveStandbyTramp?.Equals(ids.Carrier) == true)
    {
      return new Advice(
        $"{Prefix} Ugly standby open — Accept standby or Refuse (refuse ≠ premium hit)",
        soft);
    }

    if (player.Manifest.Used >= 1m)
    {
      return new Advice($"{Prefix} Manifest loaded — Depart", soft);
    }

    var hub = session.CurrentSystemId;
    var spots = CaptainJobBoard.ListLiveFreight(sim, ids, player.DefaultProfile, hub, take: 24);
    var atDock = spots.FirstOrDefault(s => s.AtOrigin && s.Margin > 0m)
                  ?? spots.FirstOrDefault(s => s.AtOrigin);
    if (atDock is not null)
    {
      return new Advice(
        $"{Prefix} Spot at dock — Accept {atDock.SkuLabel} → {atDock.DestName} (Δ{atDock.Margin:0.#})",
        soft);
    }

    var remote = spots.FirstOrDefault(s => !s.AtOrigin && s.Margin > 8m)
                 ?? spots.FirstOrDefault(s => !s.AtOrigin);
    if (remote is not null)
    {
      return new Advice(
        $"{Prefix} Travel empty → {remote.OriginName} ({remote.DistanceHint}) for {remote.SkuLabel} · steam risk",
        soft);
    }

    if (entry is { OverhaulDue: true } or { BurnedOut: true }
        || (entry is { LifeFraction: >= 0.72m }
            && cash >= ids.Registry.QuoteElectiveOverhaul(entry) + 40m))
    {
      return new Advice($"{Prefix} Drive pressure — {recovery}", soft);
    }

    return new Advice($"{Prefix} Scan Spot / Charters · Wait if weather is thin", soft);
  }

  public static string GroundingCause(ShipRegistryEntry? entry)
  {
    if (entry is null)
    {
      return "registry unknown";
    }

    if (entry.BurnedOut)
    {
      return "burned-out";
    }

    if (entry.Suspended)
    {
      return "suspended";
    }

    if (!entry.Insured)
    {
      return "uninsured";
    }

    if (entry.PremiumArrearsDays > 0)
    {
      return $"arrears {entry.PremiumArrearsDays}d";
    }

    if (entry.OverhaulDue)
    {
      return "overhaul-due";
    }

    if (!entry.CanOperate)
    {
      return entry.StandingLabel;
    }

    return entry.StandingLabel;
  }

  private static string RecoveryCta(
    ShipRegistryEntry? entry,
    decimal cash,
    decimal premium,
    decimal payable,
    CampaignWorld.Ids ids,
    bool operating)
  {
    if (entry is null)
    {
      return "check Registry";
    }

    if (entry.BurnedOut || entry.OverhaulDue)
    {
      var bill = entry.BurnedOut
        ? ids.Registry.QuoteBurnoutOverhaul(entry)
        : ids.Registry.QuoteElectiveOverhaul(entry);
      return cash >= bill + 40m
        ? "Request overhaul"
        : $"overhaul needs ~{bill + 40m:0} cash (have {cash:0})";
    }

    var rateHint = operating ? $"{premium:0.#}/d" : $"{premium:0.#}/d idle";
    if (!entry.Insured || entry.PremiumArrearsDays > 0 || entry.Suspended || payable > 0.0001m)
    {
      var need = Math.Max(payable, premium);
      return cash + 0.0001m >= need
        ? $"Settle premium (payable {payable:0.#} · accrues {rateHint})"
        : $"premium payable {payable:0.#} · accrues {rateHint} — cash short ({cash:0})";
    }

    return $"premium accrues {rateHint} (settle when due)";
  }
}
