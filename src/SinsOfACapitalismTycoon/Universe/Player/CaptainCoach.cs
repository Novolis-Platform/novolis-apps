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
      return new Advice($"{Prefix} Depart staged hold", soft);
    }

    var hub = session.CurrentSystemId;
    var board = CaptainJobBoard.ListSpot(
      sim, ids, player.DefaultProfile, hub, dockOnly: player.DockBoardOnly, mesh: ids.Mesh);
    var live = CaptainJobBoard.ListLiveFreight(sim, ids, player.DefaultProfile, hub, take: 24);
    var (offers, _) = BerthOfferBoard.Build(board, live, dockBoardOnly: player.DockBoardOnly);
    var top = offers.FirstOrDefault();
    if (top is { Kind: BerthOfferKind.Local, Spot: { } local })
    {
      return new Advice(
        $"{Prefix} {top.Band} local {local.SkuLabel} → {local.DestName} — Accept at dock",
        soft);
    }

    if (top is { Kind: BerthOfferKind.Rumor, Spot: { } rumor })
    {
      return new Advice(
        $"{Prefix} Steam empty → {rumor.OriginName} ({top.Band} rumor · {rumor.SkuLabel})",
        soft);
    }

    if (top is { Kind: BerthOfferKind.Wait })
    {
      return new Advice($"{Prefix} Hold berth — Wait (~{top.WaitDaysHint ?? 2}d) or scan charters", soft);
    }

    if (entry is { OverhaulDue: true } or { BurnedOut: true }
        || (entry is { LifeFraction: >= 0.72m }
            && cash >= ids.Registry.QuoteElectiveOverhaul(entry) + 40m))
    {
      return new Advice($"{Prefix} Drive pressure — {recovery}", soft);
    }

    return new Advice($"{Prefix} Scan berth offers · Wait if weather is thin", soft);
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
