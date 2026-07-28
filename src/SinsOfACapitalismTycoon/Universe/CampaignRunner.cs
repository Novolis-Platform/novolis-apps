using System.Diagnostics;
using System.Text;
using Novolis.Economy;
using Novolis.Economy.Agents;
using Novolis.Economy.Logistics;
using Novolis.Economy.Production;
using Novolis.Economy.Simulation;
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
    PlayerControlState? Player = null);

  /// <summary>Interactive captain session — pause when a decision is needed (or each day).</summary>
  public sealed class LiveSession
  {
    private readonly Stopwatch _sw = Stopwatch.StartNew();
    private readonly CampaignDramaHost _dramaHost;
    private readonly OpportunitiesPool _opportunities;
    private readonly PlayerTutorialHost? _tutorial;
    private readonly ManualResetEventSlim _dayGate = new(false);
    private long _remaining;
    private bool _completed;
    private bool _waiting;

    public LiveSession(
      ulong seed,
      long hours,
      bool drama,
      bool playerControl,
      bool autopilot,
      bool localBoard = true)
    {
      ClaimsPulse.ResetSeen();
      Milestones = new MilestoneLog();
      Biographies = new ShipBiographyLog();
      var (sim, ids) = CampaignWorld.Create(seed);
      Sim = sim;
      Ids = ids;
      ids.Reputation = new ReputationLedger();
      ids.Escrow = new EscrowBook();
      Player = new PlayerControlState
      {
        Enabled = playerControl,
        Autopilot = autopilot,
        LocalBoardOnly = localBoard,
      };
      Agents = SinsAgents.Create(sim, ids, Milestones, Biographies, playerControl ? Player : null);
      _opportunities = new OpportunitiesPool(ids, Milestones, ids.Reputation);
      if (playerControl)
      {
        _opportunities.PreferTramp = ids.Carrier;
        Player.Opportunities = _opportunities;
      }

      _dramaHost = new CampaignDramaHost(ids, Milestones, _opportunities, ids.Reputation, drama);
      _tutorial = playerControl ? new PlayerTutorialHost(ids, Milestones, Player) : null;
      Credits = new CreditCirculation(sim);
      Credits.SetFirmNames(ids.Firms);
      Credits.SetSkuIds(ids.Ore, ids.Parts, ids.Goods, ids.Fuel);
      OpeningLiquid = Credits.LiquidStock;
      RequestedHours = hours;
      _remaining = hours;
      DramaEnabled = drama;
      // Realtime until Calypso needs James — not a forced click every day.
      PauseMode = playerControl ? CaptainPauseMode.UntilDecision : CaptainPauseMode.Never;
    }

    public EconomySimulation Sim { get; }
    public CampaignWorld.Ids Ids { get; }
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

    /// <summary>Legacy: true maps to <see cref="CaptainPauseMode.EveryDay"/>.</summary>
    public bool PauseBetweenDays
    {
      get => PauseMode == CaptainPauseMode.EveryDay;
      set => PauseMode = value ? CaptainPauseMode.EveryDay : CaptainPauseMode.Never;
    }

    public event Action? DayEnded;
    public event Action? AwaitingDecision;

    public string CurrentHubSystemId
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

    public IReadOnlyList<CaptainJobBoard.SpotCandidate> ListJobs(int take = 16) =>
      CaptainJobBoard.ListSpot(
        Sim, Ids, Player.DefaultProfile, CurrentHubSystemId,
        berthOnly: Player.LocalBoardOnly,
        take: take);

    public IReadOnlyList<CaptainJobBoard.CharterCandidate> ListCharters() =>
      CaptainJobBoard.ListCharters(Sim, Ids, Player, CurrentHubSystemId);

    /// <summary>
    /// True when Calypso is idle on berth (or grounded) and James must act.
    /// Underway / pending departure / queued orders keep time flowing.
    /// </summary>
    public bool NeedsPlayerDecision()
    {
      if (!Player.Enabled || Player.Autopilot)
      {
        return false;
      }

      var world = Sim.State.World;
      var firm = Ids.Carrier;
      var underway = world.Shipments.Any(s =>
        !s.IsLegacy && s.FirmId.Equals(firm) && s.Status == ShipmentStatus.InTransit);
      if (underway
          || world.PendingPlanShipments.Any(p => p.FirmId.Equals(firm))
          || world.PendingPlanRepositions.Any(p => p.FirmId.Equals(firm))
          || Player.Orders.Count > 0)
      {
        return false;
      }

      // Docked idle (any LastDecision — including refuse/fail lines) needs James.
      return true;
    }

    /// <summary>Advance one day, then pause (bearings / Step 1d).</summary>
    public void StepDay()
    {
      PauseMode = CaptainPauseMode.EveryDay;
      _dayGate.Set();
    }

    /// <summary>Keep time flowing until the next decision point (or horizon).</summary>
    public void Continue()
    {
      PauseMode = CaptainPauseMode.UntilDecision;
      _dayGate.Set();
    }

    /// <summary>Run straight to horizon without captain pauses.</summary>
    public void ResumeToHorizon()
    {
      PauseMode = CaptainPauseMode.Never;
      _dayGate.Set();
    }

    public void Pause() => PauseMode = CaptainPauseMode.EveryDay;

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
        await PulseDaysAsync(step).ConfigureAwait(false);
        _remaining -= step;
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

        var shouldWait = PauseMode switch
        {
          CaptainPauseMode.Never => false,
          CaptainPauseMode.EveryDay => true,
          CaptainPauseMode.UntilDecision => NeedsPlayerDecision(),
          _ => false
        };

        if (shouldWait)
        {
          _waiting = true;
          AwaitingDecision?.Invoke();
          _dayGate.Reset();
          await Task.Run(() => _dayGate.Wait(ct), ct).ConfigureAwait(false);
          _waiting = false;
          // After StepDay, restore realtime-until-decision unless user set Never.
          if (PauseMode == CaptainPauseMode.EveryDay)
          {
            PauseMode = CaptainPauseMode.UntilDecision;
          }
        }
      }

      _sw.Stop();
      Credits.CaptureFinalMilestone();
      _completed = true;
      DayEnded?.Invoke();
    }

    private async Task PulseDaysAsync(int hours)
    {
      InsurancePulse.TickMorningReinstate(Sim, Ids.Registry, Milestones);
      for (var h = 0; h < hours; h++)
      {
        var before = Sim.State.Events.Count;
        _dramaHost.TickHour(Sim);
        var ctx = new AgentContext(
          Sim,
          new DeterministicRandom(Sim.State.Seed ^ (ulong)Sim.State.Clock.HourIndex));
        AgentScheduler.TickAll(Agents.PulseOrder.ToArray(), ctx);
        Agents.RebuildPulse();
        await Sim.AdvanceAsync(SimulationDuration.FromHours(1)).ConfigureAwait(false);
        Credits.ObserveAfterPulse(before);
        ObserveDeliveries(Sim, Ids, Biographies, Milestones);
      }

      ClaimsPulse.TickDay(Sim, Ids.Registry, Ids, Milestones, Biographies);
      DriveMaintenancePulse.TickDay(Sim, Ids.Registry, Milestones);
      Ids.Escrow.TickDay(Sim, Ids, Milestones);
      JumpBandGate.TickBerthFees(Sim, Ids, Milestones);
      LienPulse.TickDay(Sim, Ids, Milestones);
      InsurancePulse.TickDay(Sim, Ids.Registry, Milestones, Credits);
      Ids.Reputation.TickDay(Sim.State.Clock.Date.DayIndex);
      _dramaHost.TickDayEnd(Sim);
      _tutorial?.TickDayEnd(Sim);
      ObserveFinalStockout(Sim, Ids, Milestones);
    }

    public Result ToResult() =>
      new(Sim, Ids, Credits, Agents, Milestones, Biographies, OpeningLiquid, RequestedHours,
        _sw.Elapsed, DramaEnabled, Player);
  }

  public static async Task<Result> RunAsync(
    ulong seed,
    long hours,
    bool quiet,
    bool drama = true,
    bool story = false,
    Action<long, long>? progress = null,
    bool playerControl = false,
    bool autopilot = false)
  {
    var session = new LiveSession(seed, hours, drama, playerControl, autopilot);
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

  private static void ObserveDeliveries(
    EconomySimulation sim,
    CampaignWorld.Ids ids,
    ShipBiographyLog bios,
    MilestoneLog milestones)
  {
    var day = sim.State.Clock.Date.DayIndex;
    foreach (var ev in sim.State.Events.TakeLast(40))
    {
      if (ev is not ShipmentDelivered delivered)
      {
        continue;
      }

      var origin = "?";
      var dest = "?";
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
  }

  private static void ObserveFinalStockout(
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
