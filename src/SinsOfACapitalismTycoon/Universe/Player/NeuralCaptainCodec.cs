using Novolis.Economy;
using Novolis.Economy.Logistics;
using Novolis.Economy.Simulation;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>
/// Fixed-size berth observation + discrete action heads for <see cref="NeuralSurvivalCaptain"/>.
/// Mirrors NeuralRacing's sensor→control split, but for tramp bridge verbs.
/// </summary>
internal static class NeuralCaptainCodec
{
  public enum ActionKind
  {
    Wait = 0,
    AcceptBestLocal = 1,
    SteamBestRumor = 2,
    Depart = 3,
    Stabilize = 4,
  }

  public static void Encode(
    Span<double> dest,
    EconomySimulation sim,
    CampaignWorld.Ids ids,
    PlayerControlState player,
    PlayerTrampAgent agent,
    IReadOnlyList<BerthOffer> offers)
  {
    if (dest.Length < NeuralCaptainBrain.InputSize)
    {
      throw new ArgumentException($"Need {NeuralCaptainBrain.InputSize} inputs.", nameof(dest));
    }

    dest.Clear();
    var entry = ids.Registry.TryGet(ids.Carrier);
    var cash = sim.State.World.Ledgers.TryGetValue(ids.Carrier, out var led)
      ? (double)led.Cash.Amount
      : 0.0;
    var day = sim.State.Clock.Date.DayIndex;
    var docked = !sim.State.World.Shipments.Any(s =>
                   !s.IsLegacy && s.FirmId.Equals(ids.Carrier) && s.Status == ShipmentStatus.InTransit)
                 && !sim.State.World.PendingPlanShipments.Any(p => p.FirmId.Equals(ids.Carrier))
                 && !sim.State.World.PendingPlanRepositions.Any(p => p.FirmId.Equals(ids.Carrier));

    dest[0] = Clamp01(cash / 12_000.0);
    dest[1] = Clamp01((double)(entry?.LienPrincipal ?? 0m) / 5_000.0);
    dest[2] = entry is null ? 0.0 : Clamp01(1.0 - (double)entry.LifeFraction);
    dest[3] = Clamp01((double)(entry?.PremiumPayable ?? 0m) / 80.0);
    dest[4] = entry?.CanOperate == true ? 1.0 : 0.0;
    dest[5] = docked ? 1.0 : 0.0;
    dest[6] = Clamp01((double)player.Manifest.Used / (double)CampaignWorld.HullCargoCapacity);
    dest[7] = Clamp01(day / 60.0);
    dest[8] = Clamp01(Math.Max(0, CampaignWorld.LienServiceGraceDays - day) / (double)CampaignWorld.LienServiceGraceDays);
    dest[9] = player.MeshBoardUnlocked ? 1.0 : 0.0;

    var locals = offers.Where(o => o.Kind == BerthOfferKind.Local).Take(2).ToList();
    var rumors = offers.Where(o => o.Kind == BerthOfferKind.Rumor).Take(2).ToList();
    dest[10] = locals.Count > 0 ? Clamp01((double)locals[0].Spot!.Margin / 40.0) : 0.0;
    dest[11] = locals.Count > 1 ? Clamp01((double)locals[1].Spot!.Margin / 40.0) : 0.0;
    dest[12] = rumors.Count > 0 ? Clamp01((double)rumors[0].Spot!.Margin / 40.0) : 0.0;
    dest[13] = rumors.Count > 1 ? Clamp01((double)rumors[1].Spot!.Margin / 40.0) : 0.0;
    dest[14] = offers.Any(o => o.Kind == BerthOfferKind.Wait) ? 1.0 : 0.0;
    dest[15] = Clamp01(Math.Abs((double)agent.CurrentHub.GetHashCode()) / int.MaxValue);
  }

  public static ActionKind DecodeAction(ReadOnlySpan<double> output)
  {
    var best = 0;
    var bestVal = double.NegativeInfinity;
    var n = Math.Min(output.Length, NeuralCaptainBrain.OutputSize);
    for (var i = 0; i < n; i++)
    {
      if (output[i] > bestVal)
      {
        bestVal = output[i];
        best = i;
      }
    }

    return (ActionKind)best;
  }

  /// <summary>Argmax over only legal berth verbs (no empty Depart / empty Accept).</summary>
  public static ActionKind PickLegalAction(
    ReadOnlySpan<double> output,
    bool canDepart,
    bool canAcceptLocal,
    bool canSteamRumor)
  {
    var best = (int)ActionKind.Wait;
    var bestVal = double.NegativeInfinity;
    var n = Math.Min(output.Length, NeuralCaptainBrain.OutputSize);
    for (var i = 0; i < n; i++)
    {
      var kind = (ActionKind)i;
      var legal = kind switch
      {
        ActionKind.Depart => canDepart,
        ActionKind.AcceptBestLocal => canAcceptLocal,
        ActionKind.SteamBestRumor => canSteamRumor,
        ActionKind.Stabilize => true,
        ActionKind.Wait => true,
        _ => false,
      };
      if (!legal)
      {
        continue;
      }

      if (output[i] > bestVal)
      {
        bestVal = output[i];
        best = i;
      }
    }

    return (ActionKind)best;
  }

  static double Clamp01(double v) => Math.Clamp(v, 0.0, 1.0);
}
