using System.Diagnostics;
using System.Text;
using Novolis.Economy;
using Novolis.Economy.Agents;
using Novolis.Economy.Finance;
using Novolis.Economy.Logistics;
using Novolis.Economy.Production;
using Novolis.Economy.Simulation;
using SinsOfACapitalismTycoon.Ui;
using Novolis.Simulation.Mesh;
using SinsOfACapitalismTycoon.Universe.Mesh.Sins;
using Spectre.Console;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>Runs the 100-system campaign for a requested duration.</summary>
internal static class CampaignRunner
{
  public sealed record Result(
    EconomySimulation Sim,
    CampaignWorld.Ids Ids,
    CreditCirculation Credits,
    SinsAgents.Bundle Agents,
    MilestoneLog Milestones,
    ShipBiographyLog Biographies,
    decimal OpeningLiquid,
    long RequestedHours,
    TimeSpan Wall,
    bool DramaEnabled,
    PlayerControlState? Player = null,
    bool LastTrampWon = false,
    bool LastTrampLost = false,
    TrampSurvival.Snapshot? Survival = null);

  /// <summary>Interactive captain session — pause when a decision is needed (or each day).</summary>
  public sealed class LiveSession
  {
    private readonly Stopwatch _sw = Stopwatch.StartNew();
    private readonly CampaignDramaHost _dramaHost;
    private readonly OpportunitiesPool _opportunities;
    private readonly PlayerTutorialHost? _tutorial;
    private readonly CampaignPulse _pulse;
    private readonly ManualResetEventSlim _dayGate = new(false);
    private int _continuePending;
    private readonly object _bridgeGate = new();
    private long _remaining;
    private bool _completed;
    private bool _waiting;
    private bool _warming;
    private readonly Stopwatch _paceSampleClock = Stopwatch.StartNew();
    private double _gameHoursPerRealMinute;

    public LiveSession(
      ulong seed,
      long hours,
      bool drama,
      bool playerControl,
      bool autopilot,
      bool localBoard = true,
      bool lastTramp = false)
    {
      Milestones = new MilestoneLog();
      Biographies = new ShipBiographyLog();
      Claims = new ClaimsTracker();
      Events = new SimEventCursor();
      Notices = new CampaignNoticeBus();
      Fun = new FunTelemetry();
      var (sim, ids) = CampaignWorld.Create(seed);
      Sim = sim;
      Ids = ids;
      ids.Reputation = new ReputationLedger();
      ids.Escrow = new EscrowBook();
      Player = new PlayerControlState
      {
        Enabled = playerControl,
        Autopilot = autopilot,
        DockBoardOnly = localBoard,
        LastTrampMode = lastTramp,
      };
      Agents = SinsAgents.Create(sim, ids, Milestones, Biographies, playerControl ? Player : null);
      if (lastTramp)
      {
        // No household prospects while the memoir thins the board — win is sole operable LightCommercial.
        Agents.VenturesEnabled = false;
        Agents.RebuildPulse();
      }

      _opportunities = new OpportunitiesPool(ids, Milestones, ids.Reputation);
      if (playerControl)
      {
        _opportunities.PreferTramp = ids.Carrier;
        Player.Opportunities = _opportunities;
      }

      _dramaHost = new CampaignDramaHost(ids, Milestones, _opportunities, ids.Reputation, drama);
      _tutorial = playerControl ? new PlayerTutorialHost(ids, Milestones, Player) : null;
      _pulse = new CampaignPulse(this, _dramaHost, _tutorial);
      Credits = new CreditCirculation(new EconomySimulationCreditSource(sim));
      Credits.SetFirmNames(ids.Firms);
      Credits.SetSkuIds(ids.Ore, ids.Parts, ids.Goods, ids.Fuel);
      OpeningLiquid = Credits.LiquidStock;
      RequestedHours = hours;
      _remaining = hours;
      DramaEnabled = drama;
      // Sticky attention defaults to RunAlways (no hard pause); HardPause restores UntilDecision waits.
      PauseMode = playerControl ? CaptainPauseMode.UntilDecision : CaptainPauseMode.Never;
      if (playerControl)
      {
        Player.Attention = DecisionAttention.RunAlways;
        Player.SimSpeedScale = 1.0;
      }

      CaptureBridge();
    }

    public EconomySimulation Sim { get; }
    public CampaignWorld.Ids Ids { get; }
    public ClaimsTracker Claims { get; }
    public SimEventCursor Events { get; }
    public CampaignNoticeBus Notices { get; }
    public FunTelemetry Fun { get; }
    public PlayerTutorialHost? Tutorial => _tutorial;
    public CreditCirculation Credits { get; }
    public SinsAgents.Bundle Agents { get; }
    public MilestoneLog Milestones { get; }
    public ShipBiographyLog Biographies { get; }
    public PlayerControlState Player { get; }
    public decimal OpeningLiquid { get; }
    public long RequestedHours { get; }
    public long HoursDone => RequestedHours - _remaining;
    public bool DramaEnabled { get; }
    public CaptainPauseMode PauseMode { get; set; }
    public bool IsWaitingForCaptain => _waiting;
    public bool IsComplete => _completed || _remaining <= 0;

    /// <summary>Recent EMA: simulated game hours advanced per wall-clock minute.</summary>
    public double GameHoursPerRealMinute => _gameHoursPerRealMinute;

    /// <summary>Session average since live start (includes pauses).</summary>
    public double SessionGameHoursPerRealMinute
    {
      get
      {
        var mins = _sw.Elapsed.TotalMinutes;
        return mins < 0.01 ? 0 : HoursDone / mins;
      }
    }

    public string PaceLine => SessionClock.FormatPace(_gameHoursPerRealMinute);

    /// <summary>
    /// True when UI speed is max and we are not rebuilding from a save checkpoint.
    /// Allows coarser NPC / mesh work while keeping Calypso ticks hourly.
    /// </summary>
    internal bool PreferMaxSpeedThroughput =>
      !_warming && Player.SimSpeedScale >= 0.99;

    /// <summary>Record a sim pulse for the pace estimate (call after hours advance).</summary>
    internal void RecordPaceSample(int gameHoursAdvanced)
    {
      if (gameHoursAdvanced <= 0)
      {
        return;
      }

      var elapsedMin = _paceSampleClock.Elapsed.TotalMinutes;
      _paceSampleClock.Restart();
      if (elapsedMin < 1e-6)
      {
        elapsedMin = 1e-6;
      }

      var instant = gameHoursAdvanced / elapsedMin;
      _gameHoursPerRealMinute = _gameHoursPerRealMinute <= 0
        ? instant
        : (_gameHoursPerRealMinute * 0.6) + (instant * 0.4);
    }

    /// <summary>Legacy: true maps to <see cref="CaptainPauseMode.EveryDay"/>.</summary>
    public bool PauseBetweenDays
    {
      get => PauseMode == CaptainPauseMode.EveryDay;
      set => PauseMode = value ? CaptainPauseMode.EveryDay : CaptainPauseMode.Never;
    }

    public event Action? DayEnded;
    public event Action? AwaitingDecision;

    /// <summary>Last captain projection captured on the sim path (thread-safe read).</summary>
    public CaptainBridgeModel? LastBridge
    {
      get
      {
        lock (_bridgeGate)
        {
          return _lastBridge;
        }
      }
    }

    private CaptainBridgeModel? _lastBridge;

    /// <summary>Build captain projection on the calling (sim) thread; UI must bind <see cref="LastBridge"/>.</summary>
    public CaptainBridgeModel CaptureBridge()
    {
      var bridge = CaptainBridgeModel.From(this);
      lock (_bridgeGate)
      {
        _lastBridge = bridge;
      }

      return bridge;
    }

    /// <summary>Ends the run early (last-tramp win).</summary>
    internal void TruncateRemaining() => _remaining = 0;

    public string CurrentSystemId
    {
      get
      {
        var hubId = Agents.Carrier.CurrentHub;
        foreach (var hub in Ids.Bridge.Hubs)
        {
          if (hub.HubId.Equals(hubId))
          {
            return hub.SystemId;
          }
        }

        return "sol";
      }
    }

    /// <summary>Obsolete alias for <see cref="CurrentSystemId"/>.</summary>
    public string CurrentHubSystemId => CurrentSystemId;

    public IReadOnlyList<CaptainJobBoard.SpotCandidate> ListJobs(int take = 16) =>
      CaptainJobBoard.ListSpot(
        Sim, Ids, Player.DefaultProfile, CurrentSystemId,
        dockOnly: Player.DockBoardOnly,
        take: take,
        mesh: Ids.Mesh);

    public IReadOnlyList<CaptainJobBoard.CharterCandidate> ListCharters() =>
      CaptainJobBoard.ListCharters(Sim, Ids, Player, CurrentSystemId);

    public IReadOnlyList<CaptainJobBoard.MarketLot> ListMarket() =>
      CaptainJobBoard.ListMarket(Sim, Ids, CurrentHubSystemId);

    /// <summary>
    /// True when Calypso is idle on dock (or grounded) and James must act,
    /// or the intent stack is blocked waiting on fuel/cargo/input.
    /// Underway / pending departure / queued orders keep time flowing.
    /// </summary>
    public bool NeedsPlayerDecision()
    {
      if (!Player.Enabled || Player.Autopilot)
      {
        return false;
      }

      if (Player.SoftFailRaised)
      {
        return true;
      }

      if (Player.IntentStack.IsBlocked)
      {
        return true;
      }

      var world = Sim.State.World;
      var firm = Ids.Carrier;
      var underway = world.Shipments.Any(s =>
        !s.IsLegacy && s.FirmId.Equals(firm) && s.Status == ShipmentStatus.InTransit);
      if (underway
          || world.PendingPlanShipments.Any(p => p.FirmId.Equals(firm))
          || world.PendingPlanRepositions.Any(p => p.FirmId.Equals(firm))
          || Player.Orders.Count > 0
          || Player.IntentStack.Count > 0)
      {
        return false;
      }

      // Docked idle (any LastDecision — including refuse/fail lines) needs James.
      return true;
    }

    /// <summary>Sticky decision attention + sim speed (0..1).</summary>
    public void SetClock(DecisionAttention? attention = null, double? simSpeedScale = null)
    {
      if (attention is { } a)
      {
        Player.Attention = a;
      }

      if (simSpeedScale is { } s)
      {
        Player.SimSpeedScale = Math.Clamp(s, 0.0, 1.0);
      }

      // Wake a HardPause gate when switching away from hard pause.
      if (attention is DecisionAttention.RunAlways or DecisionAttention.SoftSlow)
      {
        Interlocked.Exchange(ref _continuePending, 1);
        _dayGate.Set();
      }
    }

    /// <summary>Advance one day, then pause (bearings / Step 1d).</summary>
    public void StepDay()
    {
      PauseMode = CaptainPauseMode.EveryDay;
      Interlocked.Exchange(ref _continuePending, 1);
      _dayGate.Set();
    }

    /// <summary>Wake the day gate; under HardPause keep UntilDecision waits.</summary>
    public void Continue()
    {
      if (Player.Attention == DecisionAttention.HardPause)
      {
        PauseMode = CaptainPauseMode.UntilDecision;
      }

      Interlocked.Exchange(ref _continuePending, 1);
      _dayGate.Set();
    }

    /// <summary>
    /// After enqueue + <see cref="Continue"/>, block until at least one queued order is drained
    /// (or timeout). Bridge/HTTP must not report success before the agent tick runs.
    /// </summary>
    public bool WaitForOrderDrain(int ordersBeforeEnqueue, TimeSpan timeout)
    {
      var sw = System.Diagnostics.Stopwatch.StartNew();
      while (sw.Elapsed < timeout)
      {
        if (_completed)
        {
          CaptureBridge();
          return true;
        }

        // One order processed when count drops below post-enqueue size.
        if (Player.Orders.Count < ordersBeforeEnqueue + 1)
        {
          CaptureBridge();
          return true;
        }

        Thread.Sleep(5);
      }

      CaptureBridge();
      return Player.Orders.Count < ordersBeforeEnqueue + 1;
    }

    /// <summary>Run straight to horizon without captain pauses.</summary>
    public void ResumeToHorizon()
    {
      PauseMode = CaptainPauseMode.Never;
      Interlocked.Exchange(ref _continuePending, 1);
      _dayGate.Set();
    }

    public void Pause() => PauseMode = CaptainPauseMode.EveryDay;

    /// <summary>
    /// Deterministic warm: pulse the campaign forward without captain pauses.
    /// Used to rebuild a world from a <see cref="CampaignSaveRecord"/> checkpoint.
    /// </summary>
    public async Task AdvanceHoursAsync(long hours, bool quiet = true, CancellationToken ct = default)
    {
      if (hours <= 0 || _remaining <= 0)
      {
        return;
      }

      var priorPause = PauseMode;
      var priorAuto = Player.Autopilot;
      PauseMode = CaptainPauseMode.Never;
      _warming = true;
      // Keep Calypso solvent while replaying empty captain seats.
      if (Player.Enabled)
      {
        Player.Autopilot = true;
      }

      try
      {
        var targetDone = Math.Min(HoursDone + hours, RequestedHours);
        var hoursPerPulse = 24L;
        while (_remaining > 0 && HoursDone < targetDone)
        {
          ct.ThrowIfCancellationRequested();
          var step = (int)Math.Min(hoursPerPulse, Math.Min(_remaining, targetDone - HoursDone));
          if (step <= 0)
          {
            break;
          }

          await _pulse.PulseDaysAsync(step).ConfigureAwait(false);
          _remaining -= step;
          RecordPaceSample(step);
        }
      }
      finally
      {
        _warming = false;
        Player.Autopilot = priorAuto;
        PauseMode = priorPause;
        CaptureBridge();
        if (!quiet)
        {
          EmitLiveTickers(Milestones, story: false);
        }
      }
    }

    public async Task RunAsync(
      bool quiet,
      bool story,
      Action<long, long>? progress = null,
      CancellationToken ct = default)
    {
      if (!quiet && story)
      {
        foreach (var line in VoxBank.SessionOverture(Sim.State.Seed, RequestedHours, DramaEnabled))
        {
          Console.Error.WriteLine(VoxBank.Format(line));
        }
      }

      var hoursPerPulse = 24L;
      var lastPct = -1;
      while (_remaining > 0)
      {
        ct.ThrowIfCancellationRequested();
        var step = (int)Math.Min(hoursPerPulse, _remaining);
        await _pulse.PulseDaysAsync(step).ConfigureAwait(false);
        _remaining -= step;
        RecordPaceSample(step);
        // At max speed, bridge rebuild is expensive — but always refresh while Calypso is
        // underway so Mesh FTL offline / map pose stay live.
        var maxSpeed = PreferMaxSpeedThroughput;
        var playerUnderway = Sim.State.World.Shipments.Any(s =>
          !s.IsLegacy && s.FirmId.Equals(Ids.Carrier) && s.Status == ShipmentStatus.InTransit);
        if (!maxSpeed
            || NeedsPlayerDecision()
            || playerUnderway
            || HoursDone % (24 * 3) == 0
            || _remaining <= 0)
        {
          CaptureBridge();
        }

        var done = HoursDone;
        progress?.Invoke(done, RequestedHours);
        DayEnded?.Invoke();

        if (!quiet)
        {
          EmitLiveTickers(Milestones, story);
          var pct = (int)(done * 100 / RequestedHours);
          if (pct != lastPct && pct % 10 == 0)
          {
            lastPct = pct;
            Console.Error.WriteLine($"… {DurationArg.Format(done)} / {DurationArg.Format(RequestedHours)} ({pct}%)");
          }
        }

        if (_remaining <= 0)
        {
          break;
        }

        // Wall-clock pacing (crawl ↔ max). SoftSlow throttles while a decision is needed.
        var softSlow = Player is { Enabled: true, Attention: DecisionAttention.SoftSlow }
                       && NeedsPlayerDecision();
        var paceMs = SessionClock.DelayMs(Player.SimSpeedScale, step, softSlow);
        if (paceMs > 0 && PauseMode != CaptainPauseMode.Never)
        {
          try
          {
            await Task.Delay(paceMs, ct).ConfigureAwait(false);
          }
          catch (OperationCanceledException)
          {
            throw;
          }
        }

        // Soft-fail hard-gates humans — autopilot (rule or neural) keeps trying rescue.
        if (Player.SoftFailRaised && !Player.Autopilot && PauseMode == CaptainPauseMode.Never)
        {
          PauseMode = CaptainPauseMode.UntilDecision;
        }

        var shouldWait = (Player.SoftFailRaised && !Player.Autopilot) || PauseMode switch
        {
          CaptainPauseMode.Never => false,
          CaptainPauseMode.EveryDay => true,
          // HardPause only for ordinary decisions; RunAlways / SoftSlow never hard-gate those.
          CaptainPauseMode.UntilDecision =>
            Player.Attention == DecisionAttention.HardPause && NeedsPlayerDecision(),
          _ => false
        };

        if (shouldWait)
        {
          _waiting = true;
          CaptureBridge();
          AwaitingDecision?.Invoke();
          // Captain may ResumeToHorizon during AwaitingDecision — don't Reset over that Set.
          if (PauseMode != CaptainPauseMode.Never)
          {
            // Avoid lost wakeups: Continue/Step may Set before we Reset.
            _dayGate.Reset();
            if (Interlocked.Exchange(ref _continuePending, 0) == 0)
            {
              await Task.Run(() => _dayGate.Wait(ct), ct).ConfigureAwait(false);
              Interlocked.Exchange(ref _continuePending, 0);
            }
          }

          _waiting = false;
          // After StepDay, restore sticky attention policy (not forced HardPause).
          if (PauseMode == CaptainPauseMode.EveryDay)
          {
            PauseMode = CaptainPauseMode.UntilDecision;
          }
        }
      }

      _sw.Stop();
      Credits.CaptureFinalMilestone();
      if (Player.LastTrampMode && !Player.LastTrampWon && !Player.LastTrampLost)
      {
        var snap = TrampSurvival.Capture(Ids);
        Player.LastSurvival = snap;
        if (snap.CalypsoIsSoleSurvivor)
        {
          Player.LastTrampWon = true;
          Milestones.AddOnce(Sim.State.Clock.Date.DayIndex, "last-tramp",
            $"{CampaignWorld.PlayerHullName} sole operable tramp at horizon");
        }
        else
        {
          Player.LastTrampLost = true;
          Milestones.AddOnce(Sim.State.Clock.Date.DayIndex, "last-tramp-lose",
            snap.CalypsoOperable
              ? $"horizon — {snap.OperableCount} operable tramps remain"
              : $"{CampaignWorld.PlayerHullName} down at horizon");
        }
      }

      _completed = true;
      CaptureBridge();
      DayEnded?.Invoke();
    }

    internal void EvaluateLastTramp()
    {
      var snap = TrampSurvival.Capture(Ids);
      Player.LastSurvival = snap;
      if (!Player.LastTrampMode || Player.LastTrampWon || Player.LastTrampLost)
      {
        return;
      }

      var day = Sim.State.Clock.Date.DayIndex;
      if (snap.CalypsoIsSoleSurvivor)
      {
        Player.LastTrampWon = true;
        Milestones.AddOnce(day, "last-tramp",
          $"{CampaignWorld.PlayerHullName} sole operable tramp — board cleared");
        TruncateRemaining();
        try
        {
          _ = CampaignSaveStore.Default.SaveAsync(this, "auto-win").AsTask();
        }
        catch (Exception ex)
        {
          Milestones.AddOnce(day, "save-warn", $"auto-win checkpoint skipped: {ex.GetType().Name}");
        }

        return;
      }

      if (!snap.CalypsoOperable && Player.SoftFailRaised)
      {
        Player.LastTrampLost = true;
        Milestones.AddOnce(day, "last-tramp-lose",
          $"{CampaignWorld.PlayerHullName} grounded; {snap.OperableCount} rivals still operate");
      }
    }

    public Result ToResult() =>
      new(Sim, Ids, Credits, Agents, Milestones, Biographies, OpeningLiquid, RequestedHours,
        _sw.Elapsed, DramaEnabled, Player,
        Player.LastTrampWon, Player.LastTrampLost, Player.LastSurvival ?? TrampSurvival.Capture(Ids));

    /// <summary>Write a Json storage checkpoint (seed + hours; world rebuilt on load).</summary>
    public ValueTask<CampaignSaveRecord> SaveCheckpointAsync(string? label = null, CancellationToken ct = default) =>
      CampaignSaveStore.Default.SaveAsync(this, label, ct);

    /// <summary>Build a live session and warm it to a saved checkpoint.</summary>
    public static async Task<LiveSession> FromSaveAsync(
      CampaignSaveRecord save,
      CancellationToken ct = default)
    {
      var session = new LiveSession(
        save.Seed,
        save.HorizonHours,
        save.Drama,
        playerControl: save.Player,
        autopilot: save.Autopilot,
        localBoard: save.DockBoardOnly,
        lastTramp: save.LastTramp);

      session.Player.MeshBoardUnlocked = save.MeshBoardUnlocked;

      if (save.HoursDone > 0)
      {
        await session.AdvanceHoursAsync(save.HoursDone, quiet: true, ct).ConfigureAwait(false);
      }

      // Prefer explicit save flag; also unlock from payday milestones after warm replay.
      if (save.MeshBoardUnlocked)
      {
        session.Player.MeshBoardUnlocked = true;
      }

      MeshBoardUnlock.Sync(session.Player, session.Milestones);

      if (save.HasIntegrity)
      {
        var cash = session.Sim.State.World.Ledgers.TryGetValue(session.Ids.Carrier, out var ledger)
          ? ledger.Cash.Amount
          : 0m;
        var day = session.Sim.State.Clock.Date.DayIndex;
        var hash = session.Sim.State.Hash;
        if (hash != save.SimHash
            || day != save.DayIndex
            || Math.Abs(cash - save.OpsCash) > 0.01m)
        {
          throw new InvalidOperationException(
            $"Save integrity failed after replay: expected hash={save.SimHash:X16} day={save.DayIndex} cash={save.OpsCash:0.##}; "
            + $"got hash={hash:X16} day={day} cash={cash:0.##}. "
            + "Checkpoint is seed→hours replay — simulation drift or policy change invalidated this save.");
        }
      }

      // Re-apply terminal flags in case warm stopped early / late relative to evaluate.
      session.Player.LastTrampWon = save.LastTrampWon;
      session.Player.LastTrampLost = save.LastTrampLost;
      session.Player.LastSurvival = TrampSurvival.Capture(session.Ids);
      session.CaptureBridge();
      return session;
    }
  }

  public static async Task<Result> RunAsync(
    ulong seed,
    long hours,
    bool quiet,
    bool drama = true,
    bool story = false,
    Action<long, long>? progress = null,
    bool playerControl = false,
    bool autopilot = false,
    bool lastTramp = false)
  {
    var session = new LiveSession(seed, hours, drama, playerControl, autopilot, lastTramp: lastTramp);
    session.PauseMode = CaptainPauseMode.Never;
    await session.RunAsync(quiet, story, progress).ConfigureAwait(false);
    return session.ToResult();
  }

  private static void EmitLiveTickers(MilestoneLog milestones, bool story)
  {
    foreach (var m in milestones.DrainNew())
    {
      var line = VoxBank.ForMilestone(m.Kind, m.Detail);
      if (story)
      {
        Console.Error.WriteLine($"d{m.Day} {VoxBank.Format(line)}");
      }
      else
      {
        Console.Error.WriteLine($"MILESTONE: {m.Kind} d{m.Day} {m.Detail}");
      }
    }
  }

  internal static void ObserveDeliveries(
    EconomySimulation sim,
    CampaignWorld.Ids ids,
    ShipBiographyLog bios,
    MilestoneLog milestones,
    SimEventCursor? events = null)
  {
    var day = sim.State.Clock.Date.DayIndex;
    var world = sim.State.World;
    var recent = events is null
      ? sim.State.Events.TakeLast(80).ToArray()
      : events.Since(sim).ToArray();
    foreach (var ev in recent)
    {
      if (ev is not ShipmentDelivered delivered)
      {
        continue;
      }

      ResolveDeliveryHubs(world, recent, delivered.ShipmentId, out var origin, out var dest);
      bios.Record(
        day,
        delivered.FirmId,
        origin,
        dest,
        CampaignWorld.SkuLabel(delivered.ProductId, ids),
        delivered.Quantity.Value,
        TransitProfile.StandardCommercial,
        0m,
        "delivered");
      if (delivered.ProductId.Equals(ids.Goods))
      {
        ids.Reputation.ObserveFinalDelivery(delivered.FirmId);
      }

      if (delivered.FirmId.Equals(ids.MegaHauler))
      {
        milestones.AddOnce(day, "mega",
          $"Bulk River delivered {delivered.Quantity.Value:0.#} {CampaignWorld.SkuLabel(delivered.ProductId, ids)}");
      }
    }

    events?.AdvanceToEnd(sim);
  }

  private static void ResolveDeliveryHubs(
    EconomyWorld world,
    IReadOnlyList<object> recent,
    Guid shipmentId,
    out string origin,
    out string dest)
  {
    origin = "?";
    dest = "?";
    var hubs = new List<string>();
    foreach (var ev in recent)
    {
      if (ev is ShipmentHubArrived arrived && arrived.ShipmentId == shipmentId)
      {
        var name = world.Hubs.TryGetValue(new TransportHubId(arrived.HubId), out var h)
          ? h.Name
          : "?";
        hubs.Add(name);
      }
    }

    if (hubs.Count == 0)
    {
      return;
    }

    dest = hubs[^1];
    origin = hubs.Count > 1 ? hubs[0] : hubs[^1];
  }

  internal static void ObserveFinalStockout(
    EconomySimulation sim,
    CampaignWorld.Ids ids,
    MilestoneLog milestones)
  {
    var day = sim.State.Clock.Date.DayIndex;
    decimal goods = 0m;
    foreach (var site in ids.Sites.Values.Where(s =>
               s.Hub.Role is SystemRole.Capital or SystemRole.Inhabited))
    {
      goods += sim.State.World.Inventory.GetQuantity(
        new InventoryKey(ids.Station, site.Hub.LocationId, ids.Goods)).Value;
    }

    if (goods < 8m)
    {
      milestones.AddOnce(day, "stockout", $"Final shelves thin ({goods:0.#})");
    }
  }

  public static string FormatReport(Result result)
  {
    var sb = new StringBuilder();
    var writer = new StringWriter(sb);
    var console = AnsiConsole.Create(new AnsiConsoleSettings
    {
      Out = new AnsiConsoleOutput(writer),
      Ansi = AnsiSupport.Detect,
      Interactive = InteractionSupport.No
    });
    SpectreHeadlessReport.Write(console, result);
    return sb.ToString();
  }
}
