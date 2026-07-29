using System.Collections.Immutable;

namespace SinsOfACapitalismTycoon.Universe.Mesh;

/// <summary>Hourly drone progress, loss, and arrival credits.</summary>
public static class DroneTickEngine
{
  public static MeshState Tick(MeshState state)
  {
    if (state.Drones.IsDefaultOrEmpty)
    {
      return state;
    }

    var nextDrones = ImmutableArray.CreateBuilder<InFlightDrone>();
    var retries = new List<PendingLaunch>();
    foreach (var drone in state.Drones)
    {
      if (ShouldLose(state, drone))
      {
        var pk = MeshState.PacketKey(drone.PacketId);
        var losses = state.PacketLossCounts.TryGetValue(pk, out var c) ? c : 0;
        state = state with
        {
          PacketLossCounts = state.PacketLossCounts.SetItem(pk, losses + 1),
          Stats = state.Stats with { DronesLost = state.Stats.DronesLost + 1 },
        };
        retries.Add(new PendingLaunch(
          drone.PacketId,
          drone.From,
          drone.To,
          drone.RemainingPathAfterArrival,
          drone.IsFloodHop,
          drone.Priority));
        continue;
      }

      var remaining = drone.RemainingHours - 1;
      if (remaining > 0)
      {
        nextDrones.Add(drone with { RemainingHours = remaining });
        continue;
      }

      state = OnArrive(state, drone);
    }

    state = state with { Drones = nextDrones.ToImmutable() };
    foreach (var retry in retries)
    {
      state = MeshVisibility.EnqueueLaunch(state, retry);
    }

    return state;
  }

  private static bool ShouldLose(MeshState state, InFlightDrone drone)
  {
    var n = state.Policy.LossEveryNth;
    if (n <= 0)
    {
      return false;
    }

    if (drone.RemainingHours != 1)
    {
      return false;
    }

    var pk = MeshState.PacketKey(drone.PacketId);
    var losses = state.PacketLossCounts.TryGetValue(pk, out var c) ? c : 0;
    if (state.Policy.MaxLossesPerPacket > 0 && losses >= state.Policy.MaxLossesPerPacket)
    {
      return false;
    }

    var h = HashCode.Combine(drone.PacketId.Value);
    return (h & int.MaxValue) % n == 0;
  }

  private static MeshState OnArrive(MeshState state, InFlightDrone drone)
  {
    state = state with
    {
      Stats = state.Stats with { DronesArrived = state.Stats.DronesArrived + 1 },
    };
    // CreditNode also pushes identity mail when a mailbox is co-located.
    state = MeshVisibility.CreditNode(state, drone.PacketId, drone.To);

    if (!drone.RemainingPathAfterArrival.IsDefaultOrEmpty
        && drone.RemainingPathAfterArrival.Length > 0)
    {
      var next = drone.RemainingPathAfterArrival[0];
      var rest = drone.RemainingPathAfterArrival.Length > 1
        ? drone.RemainingPathAfterArrival.Skip(1).ToImmutableArray()
        : ImmutableArray<MeshNodeId>.Empty;
      state = MeshVisibility.EnqueueLaunch(state, new PendingLaunch(
        drone.PacketId,
        drone.To,
        next,
        rest,
        IsFloodHop: false,
        drone.Priority));
    }
    else if (drone.IsFloodHop || IsFloodPacket(state, drone.PacketId))
    {
      var pk = MeshState.PacketKey(drone.PacketId);
      if (state.FloodSeededAt.TryGetValue(pk, out var seeded) && seeded.Contains(drone.To.Value))
      {
        state = state with
        {
          FloodSeededAt = state.FloodSeededAt.SetItem(pk, seeded.Remove(drone.To.Value)),
        };
      }
    }

    return state;
  }

  private static bool IsFloodPacket(MeshState state, PacketId id) =>
    state.TryGetPacket(id, out var p)
    && p.Destination.Kind is MeshAddressKind.Identity or MeshAddressKind.Feed;
}
