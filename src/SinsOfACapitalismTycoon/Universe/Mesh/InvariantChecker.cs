using System.Text;

namespace SinsOfACapitalismTycoon.Universe.Mesh;

/// <summary>BM invariants — throw on violation.</summary>
public static class InvariantChecker
{
  public static void AssertAll(MeshState state)
  {
    var errors = Check(state);
    if (errors.Count > 0)
    {
      throw new InvalidOperationException(string.Join("; ", errors));
    }
  }

  public static IReadOnlyList<string> Check(MeshState state)
  {
    var errors = new List<string>();
    foreach (var drone in state.Drones)
    {
      if (drone.RemainingHours < 0)
      {
        errors.Add($"Drone {drone.Id.Value} has negative remaining hours.");
      }

      if (!state.Packets.ContainsKey(MeshState.PacketKey(drone.PacketId)))
      {
        errors.Add($"Drone {drone.Id.Value} references missing packet.");
      }

      if (!state.Hubs.ContainsKey(drone.From.Value) || !state.Hubs.ContainsKey(drone.To.Value))
      {
        errors.Add($"Drone {drone.Id.Value} references unknown hub.");
      }
    }

    foreach (var p in state.Pending)
    {
      if (!state.Packets.ContainsKey(MeshState.PacketKey(p.PacketId)))
      {
        errors.Add($"Pending launch references missing packet {p.PacketId.Value}.");
      }
    }

    foreach (var kv in state.HubCaches)
    {
      if (!state.Hubs.ContainsKey(kv.Key))
      {
        errors.Add($"Cache for unknown hub {kv.Key}.");
      }

      foreach (var pk in kv.Value)
      {
        if (!state.Packets.ContainsKey(pk))
        {
          errors.Add($"Cache at {kv.Key} references missing packet {pk}.");
        }
      }
    }

    foreach (var kv in state.BandwidthUsedThisHour)
    {
      if (!state.Hubs.TryGetValue(kv.Key, out var hub))
      {
        errors.Add($"Bandwidth counter for unknown hub {kv.Key}.");
        continue;
      }

      if (kv.Value > hub.PulseBandwidthPerHour)
      {
        errors.Add($"Hub {kv.Key} exceeded bandwidth {kv.Value}/{hub.PulseBandwidthPerHour}.");
      }
    }

    // Edge endpoints exist
    foreach (var e in state.Edges)
    {
      if (!state.Hubs.ContainsKey(e.From.Value) || !state.Hubs.ContainsKey(e.To.Value))
      {
        errors.Add($"Edge {e.From}→{e.To} has unknown hub.");
      }

      if (e.PulseTravelHours < 1 || e.BulkTravelHours < 1)
      {
        errors.Add($"Edge {e.From}→{e.To} has non-positive travel hours.");
      }
    }

    return errors;
  }

  public static string Describe(MeshState state)
  {
    var sb = new StringBuilder();
    sb.Append("hour=").Append(state.HourIndex);
    sb.Append(" hubs=").Append(state.Hubs.Count);
    sb.Append(" packets=").Append(state.Packets.Count);
    sb.Append(" drones=").Append(state.Drones.Length);
    sb.Append(" pending=").Append(state.Pending.Length);
    return sb.ToString();
  }
}
