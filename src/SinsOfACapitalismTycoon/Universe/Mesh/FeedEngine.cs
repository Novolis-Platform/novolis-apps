using System.Collections.Immutable;

namespace SinsOfACapitalismTycoon.Universe.Mesh.Kernel;

/// <summary>
/// Atom/RSS-style feeds: voluntary channels are pulled by subscription.
/// <see cref="MeshFeedId.Emergency"/> is mandatory — always subscribed, cannot leave,
/// and force-delivered into the feed inbox of every mailbox co-located with a holding node.
/// </summary>
internal static class FeedEngine
{
  public static MeshState EnsureMandatorySubscriptions(MeshState state, MeshIdentityId owner)
  {
    var existing = state.Subscriptions.TryGetValue(owner.Value, out var book)
      ? book
      : new MeshSubscriptionBook(owner, ImmutableHashSet<string>.Empty);
    if (existing.FeedIds.Contains(MeshFeedId.Emergency.Value)
        && state.Subscriptions.ContainsKey(owner.Value))
    {
      return state;
    }

    return state with
    {
      Subscriptions = state.Subscriptions.SetItem(
        owner.Value,
        existing with { FeedIds = existing.FeedIds.Add(MeshFeedId.Emergency.Value) }),
    };
  }

  public static MeshState Subscribe(MeshState state, MeshIdentityId owner, MeshFeedId feed)
  {
    state = EnsureMandatorySubscriptions(state, owner);
    var existing = state.Subscriptions[owner.Value];
    if (existing.FeedIds.Contains(feed.Value))
    {
      return state;
    }

    return state with
    {
      Subscriptions = state.Subscriptions.SetItem(
        owner.Value,
        existing with { FeedIds = existing.FeedIds.Add(feed.Value) }),
    };
  }

  /// <summary>Cannot remove <see cref="MeshFeedId.Emergency"/>.</summary>
  public static MeshState Unsubscribe(MeshState state, MeshIdentityId owner, MeshFeedId feed)
  {
    if (feed.IsMandatory)
    {
      return state;
    }

    if (!state.Subscriptions.TryGetValue(owner.Value, out var book))
    {
      return state;
    }

    return state with
    {
      Subscriptions = state.Subscriptions.SetItem(
        owner.Value,
        book with { FeedIds = book.FeedIds.Remove(feed.Value) }),
    };
  }

  /// <summary>Effective listen set = voluntary feeds ∪ Emergency.</summary>
  public static ImmutableHashSet<string> EffectiveFeedIds(MeshState state, MeshIdentityId owner)
  {
    var set = state.Subscriptions.TryGetValue(owner.Value, out var book)
      ? book.FeedIds
      : ImmutableHashSet<string>.Empty;
    return set.Add(MeshFeedId.Emergency.Value);
  }

  /// <summary>
  /// Pull subscribed (+ mandatory Emergency) feeds at the mailbox node into the feed inbox.
  /// </summary>
  public static MeshState Pull(MeshState state, MeshIdentityId owner)
  {
    if (!state.Mailboxes.TryGetValue(owner.Value, out var box) || !box.LinkedToNode)
    {
      return state;
    }

    var feeds = EffectiveFeedIds(state, owner);
    if (!state.NodeCaches.TryGetValue(box.LocationNodeId.Value, out var cache))
    {
      return state;
    }

    var inbox = state.FeedInboxes.TryGetValue(owner.Value, out var existing)
      ? existing
      : ImmutableHashSet<string>.Empty;
    var pulled = 0L;

    foreach (var pk in cache.Keys)
    {
      if (!state.Packets.TryGetValue(pk, out var packet))
      {
        continue;
      }

      if (packet.Destination.Kind != MeshAddressKind.Feed
          || packet.Destination.Feed is not { } feed)
      {
        continue;
      }

      if (!feeds.Contains(feed.Value))
      {
        continue;
      }

      if (inbox.Contains(pk))
      {
        continue;
      }

      inbox = inbox.Add(pk);
      pulled++;
    }

    if (pulled == 0)
    {
      return state;
    }

    return state with
    {
      FeedInboxes = state.FeedInboxes.SetItem(owner.Value, inbox),
      Stats = state.Stats with { FeedPulls = state.Stats.FeedPulls + pulled },
    };
  }

  public static MeshState PullAll(MeshState state)
  {
    foreach (var id in state.Mailboxes.Keys.ToList())
    {
      state = Pull(state, MeshIdentityId.From(id));
    }

    return state;
  }

  /// <summary>
  /// When an Emergency (or other mandatory) feed packet is visible at a node,
  /// force it into every co-located mailbox's feed inbox.
  /// </summary>
  public static MeshState ForceMandatoryAtNode(MeshState state, PacketId packetId, MeshNodeId node)
  {
    if (!state.TryGetPacket(packetId, out var packet))
    {
      return state;
    }

    if (packet.Destination.Kind != MeshAddressKind.Feed
        || packet.Destination.Feed is not { } feed
        || !feed.IsMandatory)
    {
      return state;
    }

    foreach (var box in state.Mailboxes.Values)
    {
      if (!box.LinkedToNode || !box.LocationNodeId.Equals(node))
      {
        continue;
      }

      state = ForceIntoInbox(state, box.Owner, packetId, countAsEmergency: true);
    }

    return state;
  }

  internal static MeshState ForceIntoInbox(
    MeshState state,
    MeshIdentityId owner,
    PacketId packetId,
    bool countAsEmergency)
  {
    if (state.Mailboxes.TryGetValue(owner.Value, out var box) && !box.LinkedToNode)
    {
      return state;
    }

    var key = MeshState.PacketKey(packetId);
    var inbox = state.FeedInboxes.TryGetValue(owner.Value, out var existing)
      ? existing
      : ImmutableHashSet<string>.Empty;
    if (inbox.Contains(key))
    {
      return state;
    }

    state = state with
    {
      FeedInboxes = state.FeedInboxes.SetItem(owner.Value, inbox.Add(key)),
    };
    if (countAsEmergency)
    {
      state = state with
      {
        Stats = state.Stats with { EmergencyForced = state.Stats.EmergencyForced + 1 },
      };
    }

    return state;
  }
}
