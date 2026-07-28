using System.Collections.Immutable;

namespace SinsOfACapitalismTycoon.Universe.Mesh;

/// <summary>Credit visibility and mailboxes; enqueue launches.</summary>
public static class MeshVisibility
{
  public static MeshState CreditHub(MeshState state, PacketId packet, MeshHubId hub)
  {
    var key = MeshState.PacketKey(packet);
    var hubKey = hub.Value;
    var existing = state.HubCaches.TryGetValue(hubKey, out var set)
      ? set
      : ImmutableHashSet<string>.Empty;
    if (existing.Contains(key))
    {
      return state;
    }

    var next = existing.Add(key);
    return state with
    {
      HubCaches = state.HubCaches.SetItem(hubKey, next),
      Stats = state.Stats with { CacheCredits = state.Stats.CacheCredits + 1 },
    };
  }

  public static MeshState CreditMailbox(MeshState state, PacketId packet, MeshIdentityId identity)
  {
    var key = MeshState.PacketKey(packet);
    var idKey = identity.Value;
    var existing = state.Mailboxes.TryGetValue(idKey, out var set)
      ? set
      : ImmutableHashSet<string>.Empty;
    if (existing.Contains(key))
    {
      return state;
    }

    return state with
    {
      Mailboxes = state.Mailboxes.SetItem(idKey, existing.Add(key)),
      Stats = state.Stats with { MailboxCredits = state.Stats.MailboxCredits + 1 },
    };
  }

  public static MeshState EnqueueLaunch(MeshState state, PendingLaunch launch)
  {
    // Dedupe identical pending
    foreach (var p in state.Pending)
    {
      if (p.PacketId.Equals(launch.PacketId)
          && p.From.Equals(launch.From)
          && p.To.Equals(launch.To)
          && p.IsFloodHop == launch.IsFloodHop)
      {
        return state;
      }
    }

    // Skip if already visible at destination (flood) or drone already en route same hop
    if (launch.IsFloodHop && state.IsVisibleAt(launch.PacketId, launch.To))
    {
      return state;
    }

    foreach (var d in state.Drones)
    {
      if (d.PacketId.Equals(launch.PacketId) && d.From.Equals(launch.From) && d.To.Equals(launch.To))
      {
        return state;
      }
    }

    var pendingAtHub = state.Pending.Count(p => p.From.Equals(launch.From));
    if (pendingAtHub >= state.Policy.MaxPendingPerHub)
    {
      return state with
      {
        Stats = state.Stats with { BandwidthDeferred = state.Stats.BandwidthDeferred + 1 },
      };
    }

    return state with { Pending = state.Pending.Add(launch) };
  }
}
