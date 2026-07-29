using System.Collections.Immutable;

namespace SinsOfACapitalismTycoon.Universe.Mesh;

/// <summary>
/// Mailboxes for people, households, firms, ships, and things.
/// Identity packets push only when the mailbox is co-located with a node that holds them.
/// </summary>
public static class MailboxEngine
{
  public static MeshState Register(
    MeshState state,
    MeshIdentityId owner,
    MeshNodeId location,
    MeshIdentityKind? kind = null)
  {
    var resolved = kind ?? MeshIdentityIds.TryParseKind(owner) ?? MeshIdentityKind.Thing;
    state = state with
    {
      Mailboxes = state.Mailboxes.SetItem(
        owner.Value,
        new MeshMailbox(owner, resolved, location, ImmutableHashSet<string>.Empty)),
    };
    // Every mailbox is forced onto Emergency.
    return FeedEngine.EnsureMandatorySubscriptions(state, owner);
  }

  public static MeshState Move(MeshState state, MeshIdentityId owner, MeshNodeId newLocation)
  {
    if (!state.Mailboxes.TryGetValue(owner.Value, out var box))
    {
      return Register(state, owner, newLocation);
    }

    state = state with
    {
      Mailboxes = state.Mailboxes.SetItem(owner.Value, box with { LocationNodeId = newLocation }),
    };

    if (!state.NodeCaches.TryGetValue(newLocation.Value, out var cache))
    {
      return state;
    }

    foreach (var pk in cache)
    {
      if (!state.Packets.TryGetValue(pk, out var packet))
      {
        continue;
      }

      if (packet.Destination.Kind == MeshAddressKind.Identity
          && packet.Destination.Identity is { } id
          && id.Equals(owner))
      {
        state = PushPacket(state, packet.Id, owner);
      }

      // Catch up mandatory Emergency already at the new node.
      if (packet.Destination.Kind == MeshAddressKind.Feed
          && packet.Destination.Feed is { } feed
          && feed.IsMandatory)
      {
        state = FeedEngine.ForceIntoInbox(state, owner, packet.Id, countAsEmergency: true);
      }
    }

    return state;
  }

  /// <summary>After a packet becomes visible at <paramref name="node"/>, push to co-located mailboxes.</summary>
  public static MeshState PushAtNode(MeshState state, PacketId packetId, MeshNodeId node)
  {
    if (!state.TryGetPacket(packetId, out var packet))
    {
      return state;
    }

    if (packet.Destination.Kind != MeshAddressKind.Identity
        || packet.Destination.Identity is not { } target)
    {
      return state;
    }

    if (!state.Mailboxes.TryGetValue(target.Value, out var box))
    {
      return state;
    }

    if (!box.LocationNodeId.Equals(node))
    {
      return state;
    }

    return PushPacket(state, packetId, target);
  }

  private static MeshState PushPacket(MeshState state, PacketId packetId, MeshIdentityId owner)
  {
    var key = MeshState.PacketKey(packetId);
    if (!state.Mailboxes.TryGetValue(owner.Value, out var box))
    {
      return state;
    }

    if (box.PushedPacketKeys.Contains(key))
    {
      return state;
    }

    return state with
    {
      Mailboxes = state.Mailboxes.SetItem(
        owner.Value,
        box with { PushedPacketKeys = box.PushedPacketKeys.Add(key) }),
      Stats = state.Stats with { MailboxPushes = state.Stats.MailboxPushes + 1 },
    };
  }
}
