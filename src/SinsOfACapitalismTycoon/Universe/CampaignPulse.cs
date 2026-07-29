using Novolis.Economy;
using Novolis.Economy.Agents;
using Novolis.Economy.Logistics;
using Novolis.Economy.Production;
using Novolis.Economy.Simulation;
using SinsOfACapitalismTycoon.Universe.Mesh.Kernel;
using SinsOfACapitalismTycoon.Universe.Mesh.Sins;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>
/// Hour + day commerce pulse collaborator for <see cref="CampaignRunner.LiveSession"/>.
/// Session keeps pause/gates/save; this owns the simulation clock step.
/// </summary>
internal sealed class CampaignPulse
{
  /// <summary>At UI speed 1, full NPC agent ticks every N hours (player tramp still every hour).</summary>
  private const int MaxSpeedAgentStrideHours = 12;

  private readonly CampaignRunner.LiveSession _session;
  private readonly CampaignDramaHost _dramaHost;
  private readonly PlayerTutorialHost? _tutorial;

  public CampaignPulse(
    CampaignRunner.LiveSession session,
    CampaignDramaHost dramaHost,
    PlayerTutorialHost? tutorial)
  {
    _session = session;
    _dramaHost = dramaHost;
    _tutorial = tutorial;
  }

  public async Task PulseDaysAsync(int hours)
  {
    var sim = _session.Sim;
    var ids = _session.Ids;
    var agents = _session.Agents;
    var milestones = _session.Milestones;
    var bios = _session.Biographies;
    var credits = _session.Credits;
    var player = _session.Player;
    var maxSpeed = _session.PreferMaxSpeedThroughput;

    InsurancePulse.TickMorningReinstate(sim, ids.Registry, milestones);
    agents.RebuildPulse();
    var pulse = agents.PulseOrder;
    var priorThroughput = sim.ThroughputMode;

    try
    {
      for (var h = 0; h < hours; h++)
      {
        var before = sim.State.Events.Count;
        if (!maxSpeed)
        {
          _dramaHost.TickHour(sim);
        }

        var ctx = new AgentContext(
          sim,
          new DeterministicRandom(sim.State.Seed ^ (ulong)sim.State.Clock.HourIndex));

        if (maxSpeed && h % MaxSpeedAgentStrideHours != 0)
        {
          agents.CarrierPulse.Tick(ctx);
        }
        else
        {
          AgentScheduler.TickAll(pulse, ctx);
        }

        // Sparse full economy hours; other hours only apply decisions + move ships.
        sim.ThroughputMode = maxSpeed && h % MaxSpeedAgentStrideHours != 0;

        await sim.AdvanceAsync(
          SimulationDuration.FromHours(1),
          computeFinalHash: false).ConfigureAwait(false);
        credits.ObserveAfterPulse(before);
        if (!maxSpeed || h == hours - 1)
        {
          CampaignRunner.ObserveDeliveries(sim, ids, bios, milestones);
        }

        if (!maxSpeed)
        {
          ids.Mesh = MeshMailboxSync.SyncHour(ids.Mesh, sim, ids, _session.CurrentSystemId);
          ids.Mesh = MeshPulse.TickHour(ids.Mesh);
        }
        else
        {
          // Keep FTL link state hour-accurate; skip drone flood / asserts.
          ids.Mesh = MeshMailboxSync.SyncHour(ids.Mesh, sim, ids, _session.CurrentSystemId);
          ids.Mesh = ids.Mesh with
          {
            HourIndex = ids.Mesh.HourIndex + 1,
            BandwidthUsedThisHour = ids.Mesh.BandwidthUsedThisHour.Clear(),
          };
        }
      }

      ClaimsPulse.TickDay(sim, ids.Registry, ids, milestones, bios, _session.Claims);
      DriveMaintenancePulse.TickDay(sim, ids.Registry, milestones);
      ids.Escrow.TickDay(sim, ids, milestones);
      if (!maxSpeed)
      {
        ids.Mesh = MeshGameplayPulse.TickDay(ids.Mesh, sim, ids, milestones);
      }

      JumpBandGate.TickDockFees(sim, ids, milestones);
      LienPulse.TickDay(sim, ids, milestones);
      InsurancePulse.TickDay(sim, ids.Registry, milestones, credits);
      if (player.LastTrampMode)
      {
        LastTrampPressure.TickDay(sim, ids, milestones);
      }

      ids.Reputation.TickDay(sim.State.Clock.Date.DayIndex);
      if (!maxSpeed)
      {
        _dramaHost.TickDayEnd(sim);
      }

      _tutorial?.TickDayEnd(sim);
      CampaignRunner.ObserveFinalStockout(sim, ids, milestones);
      _session.EvaluateLastTramp();
    }
    finally
    {
      sim.ThroughputMode = priorThroughput;
    }
  }
}
