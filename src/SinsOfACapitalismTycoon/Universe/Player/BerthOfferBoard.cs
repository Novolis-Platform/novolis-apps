using Novolis.Economy.Logistics;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>Tramp berth wagers: Local / Rumor / Wait — never an empty board face.</summary>
internal enum BerthOfferKind
{
  Local,
  Rumor,
  Wait,
}

/// <summary>One playable berth bet for the captain desk.</summary>
internal sealed record BerthOffer(
  BerthOfferKind Kind,
  string Title,
  string Band,
  string Hook,
  string Detail,
  CaptainJobBoard.SpotCandidate? Spot,
  int SpotIndex,
  int? WaitDaysHint = null);

/// <summary>Build Fat/Fair/Thin berth offers from dock/mesh spots + live freight.</summary>
internal static class BerthOfferBoard
{
  public static string MarginBand(decimal margin)
  {
    if (margin > CampaignWorld.BerthMarginFat)
    {
      return "Fat";
    }

    if (margin >= CampaignWorld.BerthMarginFair)
    {
      return "Fair";
    }

    if (margin > 0m)
    {
      return "Thin";
    }

    return "None";
  }

  /// <summary>TTD-style time-value: Priority punishes delay; Slow/bulk forgives.</summary>
  public static string TimeValueHint(TransitProfile profile, string distanceHint)
  {
    if (profile == TransitProfile.PriorityCommercial)
    {
      return "time-sensitive · delay eats pay";
    }

    if (profile == TransitProfile.SlowEconomic)
    {
      return "bulk forgives delay";
    }

    if (distanceHint.Contains("long", StringComparison.OrdinalIgnoreCase)
        || distanceHint.Contains("12", StringComparison.Ordinal))
    {
      return "long haul · clock matters";
    }

    return "standard clock";
  }

  /// <summary>
  /// Dock mode: locals + live rumors. Mesh mode: digests only; empty → Wait (honest).
  /// </summary>
  public static (IReadOnlyList<BerthOffer> Offers, IReadOnlyList<CaptainJobBoard.SpotCandidate> Spots)
    Build(
      IReadOnlyList<CaptainJobBoard.SpotCandidate> boardSpots,
      IReadOnlyList<CaptainJobBoard.SpotCandidate> liveFreight,
      bool dockBoardOnly)
  {
    var offers = new List<BerthOffer>(3);
    var spots = new List<CaptainJobBoard.SpotCandidate>(3);
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    void AddSpotOffer(CaptainJobBoard.SpotCandidate s, BerthOfferKind kind)
    {
      var key = string.IsNullOrEmpty(s.LogicalKey)
        ? $"{s.OriginSystemId}|{s.DestSystemId}|{s.SkuLabel}|{s.Quantity:0}"
        : s.LogicalKey;
      if (!seen.Add(key))
      {
        return;
      }

      spots.Add(s);
      var idx = spots.Count - 1;
      var band = MarginBand(s.Margin);
      var timeHint = TimeValueHint(s.Profile, s.DistanceHint);
      if (kind == BerthOfferKind.Local)
      {
        offers.Add(new BerthOffer(
          BerthOfferKind.Local,
          $"{band} · {s.SkuLabel} → {s.DestName}",
          band,
          $"At dock · [{s.Profile}] · {timeHint}",
          $"Pay {s.ContractPay:0} · Lift {s.LiftCost:0} · Net Δ{s.Margin:0.#} · ×{s.Quantity:0}"
          + (band == "Thin" ? " · thin runway" : band == "Fat" ? " · fat payday" : ""),
          s,
          idx));
      }
      else
      {
        offers.Add(new BerthOffer(
          BerthOfferKind.Rumor,
          $"{band} · Steam → {s.OriginName}",
          band,
          $"{s.SkuLabel} rumor · [{s.DistanceHint}] · {timeHint}",
          $"Pay {s.ContractPay:0} · Net Δ{s.Margin:0.#} · empty steam first",
          s,
          idx));
      }
    }

    var locals = boardSpots
      .Where(s => s.AtOrigin)
      .OrderByDescending(s => s.Margin)
      .ToList();

    if (dockBoardOnly)
    {
      foreach (var s in liveFreight.Where(x => x.AtOrigin).OrderByDescending(x => x.Margin))
      {
        if (locals.Count >= 2)
        {
          break;
        }

        var key = string.IsNullOrEmpty(s.LogicalKey)
          ? $"{s.OriginSystemId}|{s.DestSystemId}|{s.SkuLabel}|{s.Quantity:0}"
          : s.LogicalKey;
        if (locals.Any(l =>
              (string.IsNullOrEmpty(l.LogicalKey) ? $"{l.OriginSystemId}|{l.DestSystemId}|{l.SkuLabel}|{l.Quantity:0}" : l.LogicalKey)
                .Equals(key, StringComparison.OrdinalIgnoreCase)))
        {
          continue;
        }

        locals.Add(s);
      }
    }

    foreach (var s in locals.Take(2))
    {
      AddSpotOffer(s, BerthOfferKind.Local);
    }

    if (dockBoardOnly && offers.Count < 2)
    {
      foreach (var s in liveFreight.Where(x => !x.AtOrigin).OrderByDescending(x => x.Margin))
      {
        if (offers.Count >= 2)
        {
          break;
        }

        AddSpotOffer(s, BerthOfferKind.Rumor);
      }
    }
    else if (!dockBoardOnly && offers.Count < 2)
    {
      // Mesh digests that are remote to this berth — still Mesh truth, not live fill.
      foreach (var s in boardSpots.Where(x => !x.AtOrigin).OrderByDescending(x => x.Margin))
      {
        if (offers.Count >= 2)
        {
          break;
        }

        AddSpotOffer(s, BerthOfferKind.Rumor);
      }
    }

    if (offers.Count == 0)
    {
      var hook = dockBoardOnly
        ? "Nothing on this berth — wait or steam when a rumor appears"
        : "Mesh digests empty — switch to Dock for live berth";
      offers.Add(new BerthOffer(
        BerthOfferKind.Wait,
        "Hold berth",
        "None",
        hook,
        "Wait · next board pulse ~2d",
        Spot: null,
        SpotIndex: -1,
        WaitDaysHint: 2));
    }

    return (offers, spots);
  }
}
