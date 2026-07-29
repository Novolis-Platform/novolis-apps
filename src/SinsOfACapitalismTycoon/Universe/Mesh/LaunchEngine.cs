using System.Collections.Immutable;

namespace SinsOfACapitalismTycoon.Universe.Mesh;

/// <summary>Consume node bandwidth to turn pending launches into in-flight drones.</summary>
public static class LaunchEngine
{
  public static MeshState LaunchPending(MeshState state)
  {
    if (state.Pending.IsDefaultOrEmpty)
    {
      return state;
    }

    var ordered = state.Pending
      .OrderByDescending(p => p.Priority)
      .ThenBy(p => p.From.Value, StringComparer.Ordinal)
      .ThenBy(p => p.To.Value, StringComparer.Ordinal)
      .ToList();

    var remaining = ImmutableArray.CreateBuilder<PendingLaunch>();
    var drones = state.Drones.ToBuilder();
    var bandwidth = state.BandwidthUsedThisHour;
    var launched = 0L;
    var deferred = 0L;

    foreach (var launch in ordered)
    {
      if (!state.Nodes.TryGetValue(launch.From.Value, out var node))
      {
        continue;
      }

      if (!state.TryGetPacket(launch.PacketId, out var packet))
      {
        continue;
      }

      if (launch.IsFloodHop && state.IsVisibleAt(launch.PacketId, launch.To))
      {
        continue;
      }

      var used = bandwidth.TryGetValue(launch.From.Value, out var u) ? u : 0;
      if (used >= node.PulseBandwidthPerHour)
      {
        remaining.Add(launch);
        deferred++;
        continue;
      }

      var hours = MeshPathfinder.TravelHours(state, launch.From, launch.To, packet.Layer);
      if (hours == int.MaxValue)
      {
        continue;
      }

      hours = Math.Max(1, hours);
      drones.Add(new InFlightDrone(
        DroneId.New(),
        launch.PacketId,
        launch.From,
        launch.To,
        hours,
        launch.RemainingPathAfterArrival,
        launch.IsFloodHop,
        launch.Priority));

      bandwidth = bandwidth.SetItem(launch.From.Value, used + 1);
      launched++;
    }

    return state with
    {
      Pending = remaining.ToImmutable(),
      Drones = drones.ToImmutable(),
      BandwidthUsedThisHour = bandwidth,
      Stats = state.Stats with
      {
        DronesLaunched = state.Stats.DronesLaunched + launched,
        BandwidthDeferred = state.Stats.BandwidthDeferred + deferred,
      },
    };
  }
}
