using Novolis.Economy.Logistics;
using Novolis.Agent.Session;
using SinsOfACapitalismTycoon.Ui;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>Single execution path for Avalonia, CLI, and session LocalIpc / MCP.</summary>
internal sealed class CaptainDeskService : IGameSession
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

  public event Action<SessionDecisionEventDto>? Decision;
  public event Action<SessionChangedEventDto>? Changed;
  public event Action<SessionActionResultEventDto>? ActionResult;

  public SessionHelloResponseDto Hello() => new()
  {
    ProtocolVersion = "1.0",
    AppId = "sins-of-a-capitalism-tycoon",
    AppTitle = "Sins — Captain Desk · ST Calypso",
    ProcessId = Environment.ProcessId,
    Capabilities =
    [
      SessionMethodNames.Snapshot,
      SessionMethodNames.Actions,
      SessionMethodNames.Command,
      SessionMethodNames.Continue,
      SessionMethodNames.Subscribe,
      SessionMethodNames.Decision,
      SessionMethodNames.Changed,
      SessionMethodNames.ActionResult,
    ],
  };

  public SessionSnapshotDto Snapshot()
  {
    lock (_gate)
    {
      return BuildSnapshot();
    }
  }

  public SessionActionsResponseDto Actions()
  {
    lock (_gate)
    {
      return new() { Actions = BuildActions(_session.LastDesk ?? _session.CaptureDesk()) };
    }
  }

  public SessionCommandResultDto Continue()
  {
    lock (_gate)
    {
      _session.Continue();
      return new SessionCommandResultDto
      {
        Ok = true,
        ActionId = SessionActionIds.Continue,
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

  public SessionCommandResultDto Execute(SessionCommandDto command)
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
          SessionActionIds.Travel or "travelTo" => ExecTravel(command),
          SessionActionIds.AcceptSpot or "accept" => ExecAcceptSpot(command),
          SessionActionIds.AcceptCharter => ExecAcceptCharter(command),
          SessionActionIds.MarketBuy or "buy" => ExecMarket(command, buy: true),
          SessionActionIds.MarketSell or "sell" => ExecMarket(command, buy: false),
          SessionActionIds.Depart => ExecDepart(command),
          SessionActionIds.RefuseStandby or "refuse" => MapSimple(
            SessionActionIds.RefuseStandby, PlayerOrderKind.RefuseStandby, "refuse standby"),
          SessionActionIds.AcceptStandby => MapSimple(
            SessionActionIds.AcceptStandby, PlayerOrderKind.AcceptStandby, "accept standby"),
          SessionActionIds.Wait => MapSimple(SessionActionIds.Wait, PlayerOrderKind.Wait, "wait"),
          SessionActionIds.Premium => MapSimple(
            SessionActionIds.Premium, PlayerOrderKind.PayPremium, "premium"),
          SessionActionIds.Overhaul => MapSimple(
            SessionActionIds.Overhaul, PlayerOrderKind.RequestOverhaul, "overhaul"),
          SessionActionIds.Step => ExecStep(),
          SessionActionIds.Continue => Continue(),
          SessionActionIds.Resume => ExecResume(),
          SessionActionIds.Save => ExecSave(command),
          SessionActionIds.SetClock => ExecSetClock(command),
          SessionActionIds.CancelStack => ExecCancelStack(),
          SessionActionIds.PrepareDepart => ExecPrepareDepart(command),
          _ => Fail(actionId, "unknown", $"Unknown action '{actionId}'"),
        };
      }
      catch (Exception ex)
      {
        return Fail(actionId, "exception", ex.Message);
      }
    }
  }

  private SessionCommandResultDto ExecTravel(SessionCommandDto command)
  {
    var dest = command.Get(SessionCommandKeys.DestSystemId)?.Trim();
    if (string.IsNullOrEmpty(dest))
    {
      return Fail(SessionActionIds.Travel, PlayerActionErrorCodes.Incomplete, "destSystemId required");
    }

    var r = CaptainActions.TryTravel(_session, dest);
    if (!r.Ok)
    {
      return Fail(SessionActionIds.Travel, r.ErrorCode ?? "error", r.Message, BuildSnapshot());
    }

    return new SessionCommandResultDto
    {
      Ok = true,
      ActionId = SessionActionIds.Travel,
      Message = r.Message,
      Snapshot = BuildSnapshot(),
    };
  }

  private SessionCommandResultDto ExecAcceptSpot(SessionCommandDto command)
  {
    var spots = (_session.LastDesk ?? _session.CaptureDesk()).SpotJobs;
    var idx = command.TryGetInt(SessionCommandKeys.Index, out var parsed) ? parsed : 0;
    if (idx < 0 || idx >= spots.Count)
    {
      return Fail(SessionActionIds.AcceptSpot, "index", "Select a spot row");
    }

    var job = spots[idx];
    if (!job.AtOrigin)
    {
      var travel = CaptainActions.TryTravel(_session, job.OriginSystemId);
      return travel.Ok
        ? Ok(SessionActionIds.Travel, $"Not at load dock — {travel.Message}")
        : Fail(SessionActionIds.Travel, travel.ErrorCode ?? "error", travel.Message);
    }

    var r = CaptainActions.TryAcceptSpot(_session, job);
    return r.Ok
      ? Ok(SessionActionIds.AcceptSpot, r.Message)
      : Fail(SessionActionIds.AcceptSpot, r.ErrorCode ?? "error", r.Message);
  }

  private SessionCommandResultDto ExecAcceptCharter(SessionCommandDto command)
  {
    var list = _session.ListCharters();
    var idx = command.TryGetInt(SessionCommandKeys.Index, out var parsed) ? parsed : 0;
    if (idx < 0 || idx >= list.Count)
    {
      return Fail(SessionActionIds.AcceptCharter, "index", "Select a charter row");
    }

    var r = CaptainActions.TryAcceptCharter(_session, list[idx]);
    var action = list[idx].Kind.Equals("standby", StringComparison.OrdinalIgnoreCase)
      ? SessionActionIds.AcceptStandby
      : SessionActionIds.AcceptCharter;
    return r.Ok ? Ok(action, r.Message) : Fail(action, r.ErrorCode ?? "error", r.Message);
  }

  private SessionCommandResultDto ExecMarket(SessionCommandDto command, bool buy)
  {
    var action = buy ? SessionActionIds.MarketBuy : SessionActionIds.MarketSell;
    var lots = _session.ListMarket();
    var idx = command.TryGetInt(SessionCommandKeys.Index, out var parsed) ? parsed : 0;
    if (idx < 0 || idx >= lots.Count)
    {
      return Fail(action, "index", "Select a market lot");
    }

    var r = CaptainActions.TryMarketTrade(_session, lots[idx], buy);
    return r.Ok ? Ok(action, r.Message) : Fail(action, r.ErrorCode ?? "error", r.Message);
  }

  private SessionCommandResultDto ExecDepart(SessionCommandDto command)
  {
    var r = CaptainActions.Simple(
      _session, PlayerOrderKind.DepartManifest, sku: command.Get(SessionCommandKeys.Sku), message: "Depart queued");
    return r.Ok ? Ok(SessionActionIds.Depart, r.Message) : Fail(SessionActionIds.Depart, r.ErrorCode ?? "error", r.Message);
  }

  private SessionCommandResultDto ExecStep()
  {
    _session.StepDay();
    return Ok(SessionActionIds.Step, "step 1d");
  }

  private SessionCommandResultDto ExecResume()
  {
    _session.ResumeToHorizon();
    return Ok(SessionActionIds.Resume, "resume to horizon");
  }

  private SessionCommandResultDto ExecSetClock(SessionCommandDto command)
  {
    DecisionAttention? attention = null;
    var attentionRaw = command.Get(SessionCommandKeys.Attention);
    if (!string.IsNullOrWhiteSpace(attentionRaw)
        && DeskClock.TryParseAttention(attentionRaw, out var parsed))
    {
      attention = parsed;
    }

    double? speed = command.TryGetDouble(SessionCommandKeys.Speed, out var speedVal) ? speedVal : null;
    _session.SetClock(attention, speed);
    var msg =
      $"clock attention={DeskClock.FormatAttention(_session.Player.Attention)} speed={_session.Player.SimSpeedScale:0.##} · {_session.PaceLine}";
    return Ok(SessionActionIds.SetClock, msg);
  }

  private SessionCommandResultDto ExecCancelStack()
  {
    var r = CaptainActions.CancelStack(_session);
    return Ok(SessionActionIds.CancelStack, r.Message);
  }

  private SessionCommandResultDto ExecPrepareDepart(SessionCommandDto command)
  {
    // Default include premium when compound (missing prepare => true).
    var premium = !command.TryGetBool(SessionCommandKeys.Prepare, out var prepare) || prepare;
    var r = CaptainActions.PrepareAndDepart(
      _session, premium: premium, overhaul: false, sku: command.Get(SessionCommandKeys.Sku));
    return r.Ok
      ? Ok(SessionActionIds.PrepareDepart, r.Message)
      : Fail(SessionActionIds.PrepareDepart, r.ErrorCode ?? "error", r.Message);
  }

  private SessionCommandResultDto ExecSave(SessionCommandDto command)
  {
    var label = command.Get(SessionCommandKeys.Label);
    var record = _session.SaveCheckpointAsync(label).AsTask().GetAwaiter().GetResult();
    return Ok(SessionActionIds.Save, $"Saved {record.Label} ({record.Id:N})");
  }

  private SessionCommandResultDto MapSimple(string actionId, PlayerOrderKind kind, string message)
  {
    var r = CaptainActions.Simple(_session, kind, message: message);
    return r.Ok ? Ok(actionId, r.Message) : Fail(actionId, r.ErrorCode ?? "error", r.Message);
  }

  private SessionCommandResultDto Ok(string actionId, string message) => new()
  {
    Ok = true,
    ActionId = actionId,
    Message = message,
    Snapshot = BuildSnapshot(),
  };

  private static SessionCommandResultDto Fail(
    string actionId,
    string errorCode,
    string message,
    SessionSnapshotDto? snapshot = null) => new()
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
    Decision?.Invoke(new SessionDecisionEventDto
    {
      Day = snap.Day,
      HubId = snap.HubId,
      DecisionLine = snap.Line(SessionLineKeys.Decision),
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
      Changed?.Invoke(new SessionChangedEventDto
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
    ActionResult?.Invoke(new SessionActionResultEventDto
    {
      ActionId = last.ActionId,
      Ok = last.Ok,
      Message = last.Message,
      ErrorCode = last.ErrorCode,
      Snapshot = BuildSnapshot(),
    });
  }

  private SessionSnapshotDto BuildSnapshot()
  {
    var desk = _session.LastDesk ?? _session.CaptureDesk();
    var pause = _session.IsComplete
      ? "Complete"
      : _session.IsWaitingForCaptain
        ? "AwaitingDecision"
        : "Running";

    return new SessionSnapshotDto
    {
      Day = desk.Day,
      SeedHash = desk.HashLine,
      HubId = desk.CurrentSystemId,
      HubName = desk.CurrentSystemName,
      PauseReason = pause,
      StatusLines = new Dictionary<string, string>(StringComparer.Ordinal)
      {
        [SessionLineKeys.Voyage] = desk.VoyageLine,
        [SessionLineKeys.Hull] = desk.HullLine,
        [SessionLineKeys.Cash] = desk.CashLine,
        [SessionLineKeys.Standing] = desk.StandingLine,
        [SessionLineKeys.Decision] = desk.DecisionLine,
        [SessionLineKeys.Coach] = desk.CoachLine,
        [SessionLineKeys.SoftFail] = desk.SoftFailLine,
        [SessionLineKeys.Survival] = desk.SurvivalLine,
        [SessionLineKeys.Mesh] = desk.MeshLine,
        [SessionLineKeys.Hold] = desk.HoldLine,
        [SessionLineKeys.Pace] = desk.PaceLine,
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
        new SessionBoardDto
        {
          Id = SessionBoardIds.SpotFreight,
          Items = desk.SpotJobs.Select((j, i) => new SessionBoardItemDto
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
        new SessionBoardDto
        {
          Id = SessionBoardIds.GoodsCharters,
          Items = desk.Charters.Select((c, i) => new SessionBoardItemDto
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
        new SessionBoardDto
        {
          Id = SessionBoardIds.MarketLots,
          Items = desk.MarketLots.Select((m, i) => new SessionBoardItemDto
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
        : new SessionLastActionDto
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

  private static SessionActionDto[] BuildActions(CaptainDeskModel desk)
  {
    var travelDest = desk.TravelTargetSystemId;
    var canTravel = desk.DockedIdle && !string.IsNullOrEmpty(travelDest)
                    && !travelDest.Equals(desk.CurrentSystemId, StringComparison.OrdinalIgnoreCase);
    return
    [
      Act(SessionActionIds.Travel, "Travel", canTravel,
        desk.DockedIdle ? (string.IsNullOrEmpty(travelDest) ? "No destination" : "Already here") : "Hull busy"),
      Act(SessionActionIds.AcceptSpot, "Accept spot",
        desk.DockedIdle && desk.SpotJobs.Any(j => j.AtOrigin) && desk.ManifestUsed < CampaignWorld.HullCargoCapacity,
        desk.DockedIdle
          ? (desk.ManifestUsed >= CampaignWorld.HullCargoCapacity ? "Hold full" : "No AT-DOCK spot")
          : "Hull busy"),
      Act(SessionActionIds.AcceptCharter, "Accept charter",
        desk.DockedIdle && desk.Charters.Any(c =>
          !c.Kind.Equals("standby", StringComparison.OrdinalIgnoreCase) && c.CanAcceptHere),
        "No acceptable charter"),
      Act(SessionActionIds.MarketBuy, "Market buy", desk.DockedIdle && desk.MarketLots.Any(m => m.IsAsk),
        "No ASK lots"),
      Act(SessionActionIds.MarketSell, "Market sell", desk.DockedIdle && desk.MarketLots.Any(m => !m.IsAsk),
        "No BID lots"),
      Act(SessionActionIds.Depart, "Depart",
        desk.ManifestLines.Count > 0 && desk.DockedIdle && desk.ManifestAtCurrentDock,
        desk.ManifestLines.Count == 0
          ? "Manifest empty"
          : desk.ManifestAtCurrentDock
            ? "Hull busy"
            : "Not at load dock"),
      Act(SessionActionIds.RefuseStandby, "Refuse standby", desk.StandbyOffer, "No standby"),
      Act(SessionActionIds.AcceptStandby, "Accept standby", desk.StandbyOffer, "No standby"),
      Act(SessionActionIds.Wait, "Wait", true, null),
      Act(SessionActionIds.Premium, "Pay premium", true, null),
      Act(SessionActionIds.Overhaul, "Overhaul", true, null),
      Act(SessionActionIds.Step, "Step 1d", !desk.Complete, "Complete"),
      Act(SessionActionIds.Continue, "Continue", !desk.Complete, "Complete"),
      Act(SessionActionIds.Resume, "Resume to horizon", !desk.Complete, "Complete"),
      Act(SessionActionIds.SetClock, "Set clock", !desk.Complete, "Complete"),
      Act(SessionActionIds.PrepareDepart, "Prepare & depart",
        desk.ManifestLines.Count > 0 && desk.DockedIdle && desk.ManifestAtCurrentDock,
        "Need manifest at dock"),
      Act(SessionActionIds.CancelStack, "Cancel stack", desk.IntentStackLines.Count > 0, "Stack empty"),
      Act(SessionActionIds.Save, "Save", true, null),
    ];
  }

  private static SessionActionDto Act(string id, string label, bool enabled, string? disabledReason) => new()
  {
    Id = id,
    Label = label,
    Enabled = enabled,
    DisabledReason = enabled ? null : disabledReason,
  };
}
