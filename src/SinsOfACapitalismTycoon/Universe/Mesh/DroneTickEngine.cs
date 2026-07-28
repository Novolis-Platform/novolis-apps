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
    foreach (var drone in state.Drones)
    {
      if (ShouldLose(state, drone))
      {
        state = state with
        {
          Stats = state.Stats with { DronesLost = state.Stats.DronesLost + 1 },
        };
        // Retry from origin of this hop
        state = MeshVisibility.EnqueueLaunch(state, new PendingLaunch(
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

    return state with { Drones = nextDrones.ToImmutable() };
  }

  private static bool ShouldLose(MeshState state, InFlightDrone drone)
  {
    var n = state.Policy.LossEveryNth;
    if (n <= 0)
    {
      return false;
    }

    var h = HashCode.Combine(drone.Id.Value, state.HourIndex, drone.PacketId.Value);
    return (h & int.MaxValue) % n == 0;
  }

  private static MeshState OnArrive(MeshState state, InFlightDrone drone)
  {
    state = state with
    {
      Stats = state.Stats with { DronesArrived = state.Stats.DronesArrived + 1 },
    };
    state = MeshVisibility.CreditHub(state, drone.PacketId, drone.To);

    if (state.TryGetPacket(drone.PacketId, out var packet))
    {
      if (packet.Destination.Kind == MeshAddressKind.Identity
          && packet.Destination.Identity is { } identity)
      {
        state = MeshVisibility.CreditMailbox(state, drone.PacketId, identity);
      }
    }

    if (!drone.RemainingPathAfterArrival.IsDefaultOrEmpty
        && drone.RemainingPathAfterArrival.Length > 0)
    {
      var next = drone.RemainingPathAfterArrival[0];
      var rest = drone.RemainingPathAfterArrival.Length > 1
        ? drone.RemainingPathAfterArrival.Skip(1).ToImmutableArray()
        : ImmutableArray<MeshHubId>.Empty;
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
      // Allow FloodDispatch to fan out from arrival hub (clear seeded flag for this hub).
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
    && p.Destination.Kind is MeshAddressKind.Identity or MeshAddressKind.Public;
}
