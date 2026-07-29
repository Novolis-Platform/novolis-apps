using System.Text;

namespace SinsOfACapitalismTycoon.Universe.Mesh.Kernel;

/// <summary>BM invariants — throw on violation.</summary>
internal static class InvariantChecker
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

      if (!state.Nodes.ContainsKey(drone.From.Value) || !state.Nodes.ContainsKey(drone.To.Value))
      {
        errors.Add($"Drone {drone.Id.Value} references unknown node.");
      }
    }

    foreach (var p in state.Pending)
    {
      if (!state.Packets.ContainsKey(MeshState.PacketKey(p.PacketId)))
      {
        errors.Add($"Pending launch references missing packet {p.PacketId.Value}.");
      }
    }

    foreach (var kv in state.NodeCaches)
    {
      if (!state.Nodes.ContainsKey(kv.Key))
      {
        errors.Add($"Cache for unknown node {kv.Key}.");
      }

      foreach (var pk in kv.Value.Keys)
      {
        if (!state.Packets.ContainsKey(pk))
        {
          errors.Add($"Cache at {kv.Key} references missing packet {pk}.");
        }
      }
    }

    foreach (var kv in state.Mailboxes)
    {
      if (!state.Nodes.ContainsKey(kv.Value.LocationNodeId.Value))
      {
        errors.Add($"Mailbox {kv.Key} at unknown node {kv.Value.LocationNodeId}.");
      }
    }

    foreach (var kv in state.BandwidthUsedThisHour)
    {
      if (!state.Nodes.TryGetValue(kv.Key, out var node))
      {
        errors.Add($"Bandwidth counter for unknown node {kv.Key}.");
        continue;
      }

      if (kv.Value > node.PulseBandwidthPerHour)
      {
        errors.Add($"Node {kv.Key} exceeded bandwidth {kv.Value}/{node.PulseBandwidthPerHour}.");
      }
    }

    foreach (var e in state.Edges)
    {
      if (!state.Nodes.ContainsKey(e.From.Value) || !state.Nodes.ContainsKey(e.To.Value))
      {
        errors.Add($"Edge {e.From}→{e.To} has unknown node.");
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
    sb.Append(" nodes=").Append(state.Nodes.Count);
    sb.Append(" packets=").Append(state.Packets.Count);
    sb.Append(" drones=").Append(state.Drones.Length);
    sb.Append(" pending=").Append(state.Pending.Length);
    sb.Append(" mailboxes=").Append(state.Mailboxes.Count);
    return sb.ToString();
  }
}
