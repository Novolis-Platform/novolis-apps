using Novolis.Economy.Logistics;
using Novolis.Agent.Session;
using SinsOfACapitalismTycoon.Ui;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>
/// In-process captain verbs shared by desk service, Avalonia, and CLI.
/// Enqueues <see cref="PlayerOrder"/> and optionally advances the day gate.
/// </summary>
internal static class CaptainActions
{
  public readonly record struct Result(bool Ok, string Message, string? ErrorCode = null, bool Advanced = false);

  public static Result TryTravel(CampaignRunner.LiveSession session, string destSystemId, bool continueSession = true)
  {
    var dest = destSystemId.Trim();
    if (dest.Length == 0)
    {
      return Fail(PlayerActionErrorCodes.Incomplete, "destSystemId required");
    }

    var desk = session.LastDesk ?? session.CaptureDesk();
    if (!desk.DockedIdle)
    {
      return Fail(PlayerActionErrorCodes.Busy, "Hull busy — wait for dock");
    }

    if (dest.Equals(desk.CurrentSystemId, StringComparison.OrdinalIgnoreCase))
    {
      var already = PlayerActionResult.Fail(
        SessionActionIds.Travel, PlayerActionErrorCodes.AlreadyHere, "already at dock");
      session.Player.LastAction = already;
      return Fail(already.ErrorCode!, already.Message);
    }

    var path = RouteHighlight.BetweenSystems(session.Ids, desk.CurrentSystemId, dest);
    if (path.Count == 0)
    {
      var noRoute = PlayerActionResult.Fail(
        SessionActionIds.Travel, PlayerActionErrorCodes.NoRoute, $"no route → {dest}");
      session.Player.LastAction = noRoute;
      return Fail(noRoute.ErrorCode!, noRoute.Message);
    }

    session.Player.TravelTargetSystemId = dest;
    return Apply(
      session,
      new PlayerOrder(
        PlayerOrderKind.TravelTo,
        DestSystemId: dest,
        Profile: session.Player.DefaultProfile),
      continueSession,
      $"Travel → {dest}");
  }

  public static Result TryAcceptSpot(
    CampaignRunner.LiveSession session,
    CaptainJobBoard.SpotCandidate job,
    bool continueSession = true)
  {
    if (!job.AtOrigin)
    {
      return Fail(
        PlayerActionErrorCodes.NotAtDock,
        $"Not at load dock — travel to {job.OriginName} first");
    }

    return Apply(
      session,
      new PlayerOrder(
        PlayerOrderKind.CommitSpot,
        OriginSystemId: job.OriginSystemId,
        DestSystemId: job.DestSystemId,
        SkuLabel: job.SkuLabel,
        Quantity: job.Quantity,
        LiftLimit: job.LiftLimit,
        DestBid: job.DestBid,
        Profile: job.Profile),
      continueSession,
      $"Spot → manifest {job.Label}");
  }

  public static Result TryAcceptCharter(
    CampaignRunner.LiveSession session,
    CaptainJobBoard.CharterCandidate c,
    bool continueSession = true)
  {
    if (c.Kind.Equals("standby", StringComparison.OrdinalIgnoreCase))
    {
      return Apply(
        session,
        new PlayerOrder(PlayerOrderKind.AcceptStandby),
        continueSession,
        "accept standby");
    }

    if (!c.CanAcceptHere || string.IsNullOrEmpty(c.OriginSystemId) || string.IsNullOrEmpty(c.DestSystemId)
        || string.IsNullOrEmpty(c.SkuLabel))
    {
      return Fail(
        PlayerActionErrorCodes.NotAtDock,
        "Charter not acceptable here — travel to load dock");
    }

    return Apply(
      session,
      new PlayerOrder(
        PlayerOrderKind.CommitSpot,
        OriginSystemId: c.OriginSystemId,
        DestSystemId: c.DestSystemId,
        SkuLabel: c.SkuLabel,
        Quantity: c.Quantity,
        LiftLimit: c.LiftLimit,
        DestBid: c.DestBid,
        Profile: c.Profile),
      continueSession,
      $"Charter → manifest {c.Label}");
  }

  public static Result TryMarketTrade(
    CampaignRunner.LiveSession session,
    CaptainJobBoard.MarketLot lot,
    bool buy,
    bool continueSession = true)
  {
    var desk = session.LastDesk ?? session.CaptureDesk();
    if (!desk.DockedIdle)
    {
      return Fail(PlayerActionErrorCodes.Busy, "Hull busy — wait for dock");
    }

    if (buy && !lot.IsAsk)
    {
      return Fail("side", "Buy uses ASK rows (sellers)");
    }

    if (!buy && lot.IsAsk)
    {
      return Fail("side", "Sell uses BID rows (buyers)");
    }

    return Apply(
      session,
      new PlayerOrder(
        buy ? PlayerOrderKind.MarketBuy : PlayerOrderKind.MarketSell,
        SkuLabel: lot.SkuLabel,
        Quantity: lot.Quantity,
        LiftLimit: lot.UnitPrice,
        CounterpartyFirmId: lot.Counterparty.Value),
      continueSession,
      $"{(buy ? "Buy" : "Sell")} {lot.SkuLabel}");
  }

  public static Result Simple(
    CampaignRunner.LiveSession session,
    PlayerOrderKind kind,
    string? sku = null,
    bool continueSession = true,
    string? message = null)
  {
    var order = new PlayerOrder(kind, SkuLabel: sku);
    var msg = message ?? kind switch
    {
      PlayerOrderKind.DepartManifest => "Depart queued",
      PlayerOrderKind.Wait => "wait",
      PlayerOrderKind.RefuseStandby => "refuse standby",
      PlayerOrderKind.AcceptStandby => "accept standby",
      PlayerOrderKind.PayPremium => "premium",
      PlayerOrderKind.RequestOverhaul => "overhaul",
      _ => kind.ToString(),
    };
    return Apply(session, order, continueSession, msg);
  }

  public static Result Apply(
    CampaignRunner.LiveSession session,
    PlayerOrder order,
    bool continueSession,
    string message)
  {
    var usedBefore = session.Player.Manifest.Used;
    var ordersBefore = session.Player.Orders.Count;
    session.Player.IntentStack.Push(order, message);
    session.Player.Orders.Enqueue(order);
    if (continueSession)
    {
      session.Continue();
      // Full day pulse can exceed a few hundred ms at Max speed — wait longer, then
      // treat still-queued as success (sim is ticking; NeedsPlayerDecision is false).
      var drained = session.WaitForOrderDrain(ordersBefore, TimeSpan.FromSeconds(3));
      if (!drained && session.Player.Orders.Count > ordersBefore)
      {
        session.CaptureDesk();
        return new Result(true, $"{message} · queued", Advanced: continueSession);
      }
    }

    var usedAfter = session.Player.Manifest.Used;
    var decisionAfter = session.Agents.CarrierPulse.LastDecision;

    if (!TryInterpretOutcome(order.Kind, usedBefore, usedAfter, decisionAfter, message,
          out var ok, out var errorCode, out var finalMessage))
    {
      ok = true;
      errorCode = null;
      finalMessage = message;
    }

    if (ok)
    {
      if (decisionAfter.StartsWith("lifting", StringComparison.OrdinalIgnoreCase))
      {
        session.Player.IntentStack.MarkWaitingCargo(decisionAfter);
      }
      else if (decisionAfter.StartsWith("bunkering", StringComparison.OrdinalIgnoreCase))
      {
        session.Player.IntentStack.MarkWaitingFuel(decisionAfter);
      }
      else if (decisionAfter.StartsWith("depart ", StringComparison.OrdinalIgnoreCase)
               || decisionAfter.StartsWith("reposition", StringComparison.OrdinalIgnoreCase)
               || decisionAfter.Contains("travel", StringComparison.OrdinalIgnoreCase))
      {
        session.Player.IntentStack.MarkDone(order.Kind);
      }
      else
      {
        session.Player.IntentStack.MarkDone(order.Kind);
      }
    }
    else
    {
      session.Player.IntentStack.MarkFailed(order.Kind, finalMessage);
    }

    return ok
      ? new Result(true, finalMessage, Advanced: continueSession)
      : new Result(false, finalMessage, errorCode, Advanced: continueSession);
  }

  public static Result PrepareAndDepart(
    CampaignRunner.LiveSession session,
    bool premium,
    bool overhaul,
    string? sku = null,
    bool continueSession = true)
  {
    foreach (var order in session.Player.IntentStack.PushPrepareAndDepart(premium, overhaul, sku))
    {
      session.Player.Orders.Enqueue(order);
    }

    if (continueSession)
    {
      session.Continue();
    }

    return new Result(true, "prepare-and-depart queued", Advanced: continueSession);
  }

  public static Result CancelStack(CampaignRunner.LiveSession session)
  {
    session.Player.Orders.Clear();
    session.Player.IntentStack.Clear();
    return new Result(true, "stack cleared");
  }

  private static bool TryInterpretOutcome(
    PlayerOrderKind kind,
    decimal usedBefore,
    decimal usedAfter,
    string decisionAfter,
    string enqueueMessage,
    out bool ok,
    out string? errorCode,
    out string finalMessage)
  {
    ok = true;
    errorCode = null;
    finalMessage = enqueueMessage;
    decisionAfter ??= "";

    switch (kind)
    {
      case PlayerOrderKind.CommitSpot:
        if (usedAfter > usedBefore + 0.0001m)
        {
          ok = true;
          finalMessage = decisionAfter.StartsWith("manifest +", StringComparison.OrdinalIgnoreCase)
            ? decisionAfter
            : $"manifest +{(usedAfter - usedBefore):0} ({usedAfter:0}/{CampaignWorld.HullCargoCapacity:0})";
          return true;
        }

        if (decisionAfter.StartsWith("manifest +", StringComparison.OrdinalIgnoreCase))
        {
          ok = true;
          finalMessage = decisionAfter;
          return true;
        }

        ok = false;
        errorCode = decisionAfter.Contains("hold full", StringComparison.OrdinalIgnoreCase)
          ? PlayerActionErrorCodes.HoldFull
          : decisionAfter.Contains("not at load", StringComparison.OrdinalIgnoreCase)
            ? PlayerActionErrorCodes.NotAtDock
            : PlayerActionErrorCodes.Rejected;
        finalMessage = decisionAfter.Length > 0 ? decisionAfter : "spot not committed";
        return true;

      case PlayerOrderKind.DepartManifest:
        // Lift/bunker deferrals put the lot back and re-queue — still a successful order accept.
        if (decisionAfter.StartsWith("lifting", StringComparison.OrdinalIgnoreCase)
            || decisionAfter.StartsWith("bunkering", StringComparison.OrdinalIgnoreCase))
        {
          ok = true;
          finalMessage = decisionAfter;
          return true;
        }

        // Require a real manifest drop — stale LastDecision "depart …" must not count as success.
        if (usedAfter + 0.0001m < usedBefore
            && decisionAfter.StartsWith("depart ", StringComparison.OrdinalIgnoreCase))
        {
          ok = true;
          finalMessage = decisionAfter;
          return true;
        }

        if (usedAfter + 0.0001m < usedBefore)
        {
          ok = true;
          finalMessage = decisionAfter.Length > 0 ? decisionAfter : enqueueMessage;
          return true;
        }

        ok = false;
        errorCode = decisionAfter.Contains("empty", StringComparison.OrdinalIgnoreCase)
          ? "empty"
          : PlayerActionErrorCodes.Busy;
        finalMessage = decisionAfter.Length > 0 ? decisionAfter : "depart failed";
        return true;

      case PlayerOrderKind.TravelTo:
        if (decisionAfter.Contains("travel", StringComparison.OrdinalIgnoreCase)
            || decisionAfter.Contains("bunkering", StringComparison.OrdinalIgnoreCase)
            || decisionAfter.Contains("reposition", StringComparison.OrdinalIgnoreCase)
            || decisionAfter.Contains("awaiting departure", StringComparison.OrdinalIgnoreCase))
        {
          ok = true;
          finalMessage = decisionAfter.Length > 0 ? decisionAfter : enqueueMessage;
          return true;
        }

        if (decisionAfter.Contains("failed", StringComparison.OrdinalIgnoreCase)
            || decisionAfter.Contains("no route", StringComparison.OrdinalIgnoreCase)
            || decisionAfter.Contains("already", StringComparison.OrdinalIgnoreCase)
            || decisionAfter.Contains("busy", StringComparison.OrdinalIgnoreCase))
        {
          ok = false;
          errorCode = PlayerActionErrorCodes.Rejected;
          finalMessage = decisionAfter;
          return true;
        }

        // Still queued / not observed yet — keep optimistic enqueue message but mark incomplete wait.
        ok = false;
        errorCode = PlayerActionErrorCodes.Busy;
        finalMessage = decisionAfter.Length > 0 ? decisionAfter : "travel not started";
        return true;

      default:
        return false;
    }
  }

  private static Result Fail(string errorCode, string message) =>
    new(false, message, errorCode);
}
