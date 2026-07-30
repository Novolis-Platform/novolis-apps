using Novolis.Economy;
using Novolis.Economy.Agents;
using Novolis.Economy.Finance;
using Novolis.Economy.Logistics;
using Novolis.Economy.Production;
using Novolis.Economy.Simulation;
using Novolis.Simulation.Mesh;
using SinsOfACapitalismTycoon.Universe.Mesh.Sins;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>
/// Hour + day commerce pulse collaborator for <see cref="CampaignRunner.LiveSession"/>.
/// Hour loop stays here; day-end is an ordered <see cref="CampaignDayPipeline"/> of components.
/// </summary>
internal sealed class CampaignPulse
{
  /// <summary>At UI speed 1, full NPC agent ticks every N hours (player tramp still every hour).</summary>
  private const int MaxSpeedAgentStrideHours = 12;
  /// <summary>Autopilot horizons need denser NPC ticks so freight markets stay alive.</summary>
  private const int AutopilotMaxSpeedAgentStrideHours = 2;

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
    var agentStride = player.Autopilot ? AutopilotMaxSpeedAgentStrideHours : MaxSpeedAgentStrideHours;

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

        if (maxSpeed && h % agentStride != 0)
        {
          agents.CarrierPulse.Tick(ctx);
        }
        else
        {
          AgentScheduler.TickAll(pulse, ctx);
        }

        // Sparse full economy hours; other hours only apply decisions + move ships.
        sim.ThroughputMode = maxSpeed && h % agentStride != 0;

        await sim.AdvanceAsync(
          SimulationDuration.FromHours(1),
          computeFinalHash: false).ConfigureAwait(false);
        credits.ObserveAfterPulse(before);
        if (!maxSpeed || h == hours - 1)
        {
          CampaignRunner.ObserveDeliveries(sim, ids, bios, milestones, _session.Events);
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

      var dayCtx = new CampaignPulseContext
      {
        Sim = sim,
        Ids = ids,
        Agents = agents,
        Milestones = milestones,
        Biographies = bios,
        Credits = credits,
        Player = player,
        Claims = _session.Claims,
        Events = _session.Events,
        Notices = _session.Notices,
        Drama = _dramaHost,
        Tutorial = _tutorial,
        CurrentSystemId = () => _session.CurrentSystemId,
        EvaluateLastTramp = _session.EvaluateLastTramp,
        MaxSpeedThroughput = maxSpeed,
      };
      CampaignDayPipeline.RunDayEnd(dayCtx);
    }
    finally
    {
      sim.ThroughputMode = priorThroughput;
    }
  }
}
