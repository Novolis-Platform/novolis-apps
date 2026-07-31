using Novolis.Economy.Logistics;
using Novolis.Agent.Core;
using SinsOfACapitalismTycoon.Ui;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>Single execution path for Avalonia, CLI, and agent LocalIpc / MCP.</summary>
internal sealed class CaptainDeskService : IAgentHost
{
  private readonly CampaignRunner.LiveSession _session;
  private readonly object _gate = new();
  private PlayerActionResult? _lastEmittedAction;
  private int _changedCoalesce;

  public CaptainDeskService(CampaignRunner.LiveSession session)
  {
    _session = session ?? throw new ArgumentNullException(nameof(session));
    _session.AwaitingDecision += OnAwaitingDecision;
    _session.DayEnded += OnDayEnded;
  }

  public event Action<AgentDecisionEvent>? Decision;
  public event Action<AgentChangedEvent>? Changed;
  public event Action<AgentActionResultEvent>? ActionResult;

  public AgentHello Hello() => new()
  {
    ProtocolVersion = "1.0",
    AppId = "sins-of-a-capitalism-tycoon",
    AppTitle = "Sins — Captain Desk · ST Calypso",
    ProcessId = Environment.ProcessId,
    Capabilities =
    [
      AgentMethodNames.Snapshot,
      AgentMethodNames.Actions,
      AgentMethodNames.Command,
      AgentMethodNames.Continue,
      AgentMethodNames.Subscribe,
      AgentMethodNames.Decision,
      AgentMethodNames.Changed,
      AgentMethodNames.ActionResult,
    ],
  };

  public AgentSnapshot Snapshot()
  {
    lock (_gate)
    {
      return BuildSnapshot();
    }
  }

  public AgentActionsResponse Actions()
  {
    lock (_gate)
    {
      return new() { Actions = BuildActions(_session.LastDesk ?? _session.CaptureDesk()) };
    }
  }

  public AgentCommandResult Continue()
  {
    lock (_gate)
    {
      _session.Continue();
      return new AgentCommandResult
      {
        Ok = true,
        ActionId = AgentActionIds.Continue,
        Message = "continue",
        Snapshot = BuildSnapshot(),
      };
    }
  }

  public void Subscribe()
  {
    lock (_gate)
    {
      // Session flag reserved for future per-client filtering; SessionHost tracks connections.
    }
  }

  public AgentCommandResult Execute(AgentCommand command)
  {
    lock (_gate)
    {
      ArgumentNullException.ThrowIfNull(command);
      var actionId = command.ActionId?.Trim() ?? "";
      if (actionId.Length == 0)
      {
        return Fail(actionId, "incomplete", "ActionId required");
      }

      try
      {
        return actionId switch
        {
          AgentActionIds.Travel or "travelTo" => ExecTravel(command),
          AgentActionIds.AcceptSpot or "accept" => ExecAcceptSpot(command),
          AgentActionIds.AcceptCharter => ExecAcceptCharter(command),
          AgentActionIds.MarketBuy or "buy" => ExecMarket(command, buy: true),
          AgentActionIds.MarketSell or "sell" => ExecMarket(command, buy: false),
          AgentActionIds.Depart => ExecDepart(command),
          AgentActionIds.RefuseStandby or "refuse" => MapSimple(
            AgentActionIds.RefuseStandby, PlayerOrderKind.RefuseStandby, "refuse standby"),
          AgentActionIds.AcceptStandby => MapSimple(
            AgentActionIds.AcceptStandby, PlayerOrderKind.AcceptStandby, "accept standby"),
          AgentActionIds.Wait => MapSimple(AgentActionIds.Wait, PlayerOrderKind.Wait, "wait"),
          AgentActionIds.Premium => MapSimple(
            AgentActionIds.Premium, PlayerOrderKind.PayPremium, "premium"),
          AgentActionIds.Overhaul => MapSimple(
            AgentActionIds.Overhaul, PlayerOrderKind.RequestOverhaul, "overhaul"),
          AgentActionIds.Step => ExecStep(),
          AgentActionIds.Continue => Continue(),
          AgentActionIds.Resume => ExecResume(),
          AgentActionIds.Save => ExecSave(command),
          AgentActionIds.SetClock => ExecSetClock(command),
          AgentActionIds.CancelStack => ExecCancelStack(),
          AgentActionIds.PrepareDepart => ExecPrepareDepart(command),
          _ => Fail(actionId, "unknown", $"Unknown action '{actionId}'"),
        };
      }
      catch (Exception ex)
      {
        return Fail(actionId, "exception", ex.Message);
      }
    }
  }

  private AgentCommandResult ExecTravel(AgentCommand command)
  {
    var dest = command.Get(AgentCommandKeys.DestSystemId)?.Trim();
    if (string.IsNullOrEmpty(dest))
    {
      return Fail(AgentActionIds.Travel, PlayerActionErrorCodes.Incomplete, "destSystemId required");
    }

    var r = CaptainActions.TryTravel(_session, dest);
    if (!r.Ok)
    {
      return Fail(AgentActionIds.Travel, r.ErrorCode ?? "error", r.Message, BuildSnapshot());
    }

    return new AgentCommandResult
    {
      Ok = true,
      ActionId = AgentActionIds.Travel,
      Message = r.Message,
      Snapshot = BuildSnapshot(),
    };
  }

  private AgentCommandResult ExecAcceptSpot(AgentCommand command)
  {
    var spots = (_session.LastDesk ?? _session.CaptureDesk()).SpotJobs;
    var idx = command.TryGetInt(AgentCommandKeys.Index, out var parsed) ? parsed : 0;
    if (idx < 0 || idx >= spots.Count)
    {
      return Fail(AgentActionIds.AcceptSpot, "index", "Select a spot row");
    }

    var job = spots[idx];
    if (!job.AtOrigin)
    {
      var travel = CaptainActions.TryTravel(_session, job.OriginSystemId);
      return travel.Ok
        ? Ok(AgentActionIds.Travel, $"Not at load dock — {travel.Message}")
        : Fail(AgentActionIds.Travel, travel.ErrorCode ?? "error", travel.Message);
    }

    var r = CaptainActions.TryAcceptSpot(_session, job);
    return r.Ok
      ? Ok(AgentActionIds.AcceptSpot, r.Message)
      : Fail(AgentActionIds.AcceptSpot, r.ErrorCode ?? "error", r.Message);
  }

  private AgentCommandResult ExecAcceptCharter(AgentCommand command)
  {
    var list = _session.ListCharters();
    var idx = command.TryGetInt(AgentCommandKeys.Index, out var parsed) ? parsed : 0;
    if (idx < 0 || idx >= list.Count)
    {
      return Fail(AgentActionIds.AcceptCharter, "index", "Select a charter row");
    }

    var r = CaptainActions.TryAcceptCharter(_session, list[idx]);
    var action = list[idx].Kind.Equals("standby", StringComparison.OrdinalIgnoreCase)
      ? AgentActionIds.AcceptStandby
      : AgentActionIds.AcceptCharter;
    return r.Ok ? Ok(action, r.Message) : Fail(action, r.ErrorCode ?? "error", r.Message);
  }

  private AgentCommandResult ExecMarket(AgentCommand command, bool buy)
  {
    var action = buy ? AgentActionIds.MarketBuy : AgentActionIds.MarketSell;
    var lots = _session.ListMarket();
    var idx = command.TryGetInt(AgentCommandKeys.Index, out var parsed) ? parsed : 0;
    if (idx < 0 || idx >= lots.Count)
    {
      return Fail(action, "index", "Select a market lot");
    }

    var r = CaptainActions.TryMarketTrade(_session, lots[idx], buy);
    return r.Ok ? Ok(action, r.Message) : Fail(action, r.ErrorCode ?? "error", r.Message);
  }

  private AgentCommandResult ExecDepart(AgentCommand command)
  {
    var r = CaptainActions.Simple(
      _session, PlayerOrderKind.DepartManifest, sku: command.Get(AgentCommandKeys.Sku), message: "Depart queued");
    return r.Ok ? Ok(AgentActionIds.Depart, r.Message) : Fail(AgentActionIds.Depart, r.ErrorCode ?? "error", r.Message);
  }

  private AgentCommandResult ExecStep()
  {
    _session.StepDay();
    return Ok(AgentActionIds.Step, "step 1d");
  }

  private AgentCommandResult ExecResume()
  {
    _session.ResumeToHorizon();
    return Ok(AgentActionIds.Resume, "resume to horizon");
  }

  private AgentCommandResult ExecSetClock(AgentCommand command)
  {
    DecisionAttention? attention = null;
    var attentionRaw = command.Get(AgentCommandKeys.Attention);
    if (!string.IsNullOrWhiteSpace(attentionRaw)
        && DeskClock.TryParseAttention(attentionRaw, out var parsed))
    {
      attention = parsed;
    }

    double? speed = command.TryGetDouble(AgentCommandKeys.Speed, out var speedVal) ? speedVal : null;
    _session.SetClock(attention, speed);
    var msg =
      $"clock attention={DeskClock.FormatAttention(_session.Player.Attention)} speed={_session.Player.SimSpeedScale:0.##} · {_session.PaceLine}";
    return Ok(AgentActionIds.SetClock, msg);
  }

  private AgentCommandResult ExecCancelStack()
  {
    var r = CaptainActions.CancelStack(_session);
    return Ok(AgentActionIds.CancelStack, r.Message);
  }

  private AgentCommandResult ExecPrepareDepart(AgentCommand command)
  {
    // Default include premium when compound (missing prepare => true).
    var premium = !command.TryGetBool(AgentCommandKeys.Prepare, out var prepare) || prepare;
    var r = CaptainActions.PrepareAndDepart(
      _session, premium: premium, overhaul: false, sku: command.Get(AgentCommandKeys.Sku));
    return r.Ok
      ? Ok(AgentActionIds.PrepareDepart, r.Message)
      : Fail(AgentActionIds.PrepareDepart, r.ErrorCode ?? "error", r.Message);
  }

  private AgentCommandResult ExecSave(AgentCommand command)
  {
    var label = command.Get(AgentCommandKeys.Label);
    var record = _session.SaveCheckpointAsync(label).AsTask().GetAwaiter().GetResult();
    return Ok(AgentActionIds.Save, $"Saved {record.Label} ({record.Id:N})");
  }

  private AgentCommandResult MapSimple(string actionId, PlayerOrderKind kind, string message)
  {
    var r = CaptainActions.Simple(_session, kind, message: message);
    return r.Ok ? Ok(actionId, r.Message) : Fail(actionId, r.ErrorCode ?? "error", r.Message);
  }

  private AgentCommandResult Ok(string actionId, string message) => new()
  {
    Ok = true,
    ActionId = actionId,
    Message = message,
    Snapshot = BuildSnapshot(),
  };

  private static AgentCommandResult Fail(
    string actionId,
    string errorCode,
    string message,
    AgentSnapshot? snapshot = null) => new()
  {
    Ok = false,
    ActionId = actionId,
    ErrorCode = errorCode,
    Message = message,
    Snapshot = snapshot,
  };

  private void OnAwaitingDecision()
  {
    EmitActionResultIfNeeded();
    var snap = BuildSnapshot();
    Decision?.Invoke(new AgentDecisionEvent
    {
      Day = snap.Day,
      HubId = snap.HubId,
      DecisionLine = snap.Line(AgentLineKeys.Decision),
      Snapshot = snap,
    });
  }

  private void OnDayEnded()
  {
    EmitActionResultIfNeeded();
    // Coalesce bursty day-end / soft-fail noise.
    if (Interlocked.Increment(ref _changedCoalesce) > 1)
    {
      return;
    }

    _ = Task.Run(async () =>
    {
      await Task.Delay(25).ConfigureAwait(false);
      Interlocked.Exchange(ref _changedCoalesce, 0);
      var snap = BuildSnapshot();
      Changed?.Invoke(new AgentChangedEvent
      {
        Reason = "day-end",
        Snapshot = snap,
      });
    });
  }

  private void EmitActionResultIfNeeded()
  {
    var last = _session.Player.LastAction;
    if (last is null || ReferenceEquals(last, _lastEmittedAction))
    {
      return;
    }

    _lastEmittedAction = last;
    ActionResult?.Invoke(new AgentActionResultEvent
    {
      ActionId = last.ActionId,
      Ok = last.Ok,
      Message = last.Message,
      ErrorCode = last.ErrorCode,
      Snapshot = BuildSnapshot(),
    });
  }

  private AgentSnapshot BuildSnapshot()
  {
    var desk = _session.LastDesk ?? _session.CaptureDesk();
    var pause = _session.IsComplete
      ? "Complete"
      : _session.IsWaitingForCaptain
        ? "AwaitingDecision"
        : "Running";

    return new AgentSnapshot
    {
      Day = desk.Day,
      SeedHash = desk.HashLine,
      HubId = desk.CurrentSystemId,
      HubName = desk.CurrentSystemName,
      PauseReason = pause,
      StatusLines = new Dictionary<string, string>(StringComparer.Ordinal)
      {
        [AgentLineKeys.Voyage] = desk.VoyageLine,
        [AgentLineKeys.Hull] = desk.HullLine,
        [AgentLineKeys.Cash] = desk.CashLine,
        [AgentLineKeys.Standing] = desk.StandingLine,
        [AgentLineKeys.Decision] = desk.DecisionLine,
        [AgentLineKeys.Coach] = desk.CoachLine,
        [AgentLineKeys.SoftFail] = desk.SoftFailLine,
        [AgentLineKeys.Survival] = desk.SurvivalLine,
        [AgentLineKeys.Mesh] = desk.MeshLine,
        [AgentLineKeys.Hold] = desk.HoldLine,
        [AgentLineKeys.Pace] = desk.PaceLine,
      },
      Underway = desk.Underway,
      DockedIdle = desk.DockedIdle,
      Complete = desk.Complete,
      SoftFail = desk.SoftFail,
      StandbyOffer = desk.StandbyOffer,
      TravelTargetSystemId = desk.TravelTargetSystemId,
      RouteSystemIds = desk.RouteSystemIds.ToArray(),
      Boards =
      [
        new AgentBoard
        {
          Id = AgentBoardIds.SpotFreight,
          Items = desk.SpotJobs.Select((j, i) => new AgentBoardItem
          {
            Index = i,
            Id = $"{j.OriginSystemId}->{j.DestSystemId}:{j.SkuLabel}",
            Label = j.Label,
            Detail = j.AtOrigin
              ? $"pay {j.ContractPay:0} lift {j.LiftCost:0} Δ{j.Margin:0.#} ×{j.Quantity:0}"
              : $"intel — travel {j.OriginName} · pay {j.ContractPay:0}",
            CanAct = j.AtOrigin && desk.DockedIdle,
          }).ToArray(),
        },
        new AgentBoard
        {
          Id = AgentBoardIds.GoodsCharters,
          Items = desk.Charters.Select((c, i) => new AgentBoardItem
          {
            Index = i,
            Id = c.Kind + ":" + c.Label,
            Label = c.Label,
            Detail = c.ContractPay > 0m
              ? $"pay {c.ContractPay:0} lift {c.LiftCost:0} Δ{c.Margin:0.#} · {c.Detail}"
              : c.Detail,
            CanAct = desk.DockedIdle && (c.CanAcceptHere
              || c.Kind.Equals("standby", StringComparison.OrdinalIgnoreCase)),
          }).ToArray(),
        },
        new AgentBoard
        {
          Id = AgentBoardIds.MarketLots,
          Items = desk.MarketLots.Select((m, i) => new AgentBoardItem
          {
            Index = i,
            Id = m.Summary,
            Label = m.Summary,
            Detail = m.IsAsk ? "ASK" : "BID",
            CanAct = desk.DockedIdle,
          }).ToArray(),
        },
      ],
      Manifest = desk.ManifestLines.ToArray(),
      Actions = BuildActions(desk),
      LastAction = desk.LastAction is null
        ? null
        : new AgentLastAction
        {
          ActionId = desk.LastAction.ActionId,
          Ok = desk.LastAction.Ok,
          Message = desk.LastAction.Message,
          ErrorCode = desk.LastAction.ErrorCode,
        },
      Attention = DeskClock.FormatAttention(_session.Player.Attention),
      SimSpeedScale = _session.Player.SimSpeedScale,
      IntentStack = desk.IntentStackLines.ToArray(),
      MapX = desk.ShipMapX,
      MapY = desk.ShipMapY,
      MapVisible = desk.ShipMapVisible,
      GameHoursPerRealMinute = desk.GameHoursPerRealMinute,
      SessionGameHoursPerRealMinute = desk.SessionGameHoursPerRealMinute,
    };
  }

  private static AgentAction[] BuildActions(CaptainDeskModel desk)
  {
    var travelDest = desk.TravelTargetSystemId;
    var canTravel = desk.DockedIdle && !string.IsNullOrEmpty(travelDest)
                    && !travelDest.Equals(desk.CurrentSystemId, StringComparison.OrdinalIgnoreCase);
    return
    [
      Act(AgentActionIds.Travel, "Travel", canTravel,
        desk.DockedIdle ? (string.IsNullOrEmpty(travelDest) ? "No destination" : "Already here") : "Hull busy"),
      Act(AgentActionIds.AcceptSpot, "Accept spot",
        desk.DockedIdle && desk.SpotJobs.Any(j => j.AtOrigin) && desk.ManifestUsed < CampaignWorld.HullCargoCapacity,
        desk.DockedIdle
          ? (desk.ManifestUsed >= CampaignWorld.HullCargoCapacity ? "Hold full" : "No AT-DOCK spot")
          : "Hull busy"),
      Act(AgentActionIds.AcceptCharter, "Accept charter",
        desk.DockedIdle && desk.Charters.Any(c =>
          !c.Kind.Equals("standby", StringComparison.OrdinalIgnoreCase) && c.CanAcceptHere),
        "No acceptable charter"),
      Act(AgentActionIds.MarketBuy, "Market buy", desk.DockedIdle && desk.MarketLots.Any(m => m.IsAsk),
        "No ASK lots"),
      Act(AgentActionIds.MarketSell, "Market sell", desk.DockedIdle && desk.MarketLots.Any(m => !m.IsAsk),
        "No BID lots"),
      Act(AgentActionIds.Depart, "Depart",
        desk.ManifestLines.Count > 0 && desk.DockedIdle && desk.ManifestAtCurrentDock,
        desk.ManifestLines.Count == 0
          ? "Manifest empty"
          : desk.ManifestAtCurrentDock
            ? "Hull busy"
            : "Not at load dock"),
      Act(AgentActionIds.RefuseStandby, "Refuse standby", desk.StandbyOffer, "No standby"),
      Act(AgentActionIds.AcceptStandby, "Accept standby", desk.StandbyOffer, "No standby"),
      Act(AgentActionIds.Wait, "Wait", true, null),
      Act(AgentActionIds.Premium, "Pay premium", true, null),
      Act(AgentActionIds.Overhaul, "Overhaul", true, null),
      Act(AgentActionIds.Step, "Step 1d", !desk.Complete, "Complete"),
      Act(AgentActionIds.Continue, "Continue", !desk.Complete, "Complete"),
      Act(AgentActionIds.Resume, "Resume to horizon", !desk.Complete, "Complete"),
      Act(AgentActionIds.SetClock, "Set clock", !desk.Complete, "Complete"),
      Act(AgentActionIds.PrepareDepart, "Prepare & depart",
        desk.ManifestLines.Count > 0 && desk.DockedIdle && desk.ManifestAtCurrentDock,
        "Need manifest at dock"),
      Act(AgentActionIds.CancelStack, "Cancel stack", desk.IntentStackLines.Count > 0, "Stack empty"),
      Act(AgentActionIds.Save, "Save", true, null),
    ];
  }

  private static AgentAction Act(string id, string label, bool enabled, string? disabledReason) => new()
  {
    Id = id,
    Label = label,
    Enabled = enabled,
    DisabledReason = enabled ? null : disabledReason,
  };
}
