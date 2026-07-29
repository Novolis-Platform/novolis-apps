using SinsOfACapitalismTycoon.Universe.Mesh.Kernel;
namespace SinsOfACapitalismTycoon.Universe.Mesh.Sins;

/// <summary>Captain desk / CLI projection of mesh mailbox + feed inbox.</summary>
internal static class MeshCaptainInbox
{
  public sealed record Snapshot(
    int MailboxPushCount,
    int FeedInboxCount,
    int EmergencyCount,
    int SpotDigestCount,
    IReadOnlyList<string> RecentSubjects);

  public static Snapshot ForCaptain(MeshState mesh)
  {
    var person = MeshIdentityIds.Person(CampaignWorld.PlayerFlavorId);
    var ship = MeshIdentityIds.Ship(CampaignWorld.PlayerHullName);
    var keys = new HashSet<string>(StringComparer.Ordinal);
    var subjects = new List<(long Hour, string Subject)>();

    Collect(mesh, person, keys, subjects);
    Collect(mesh, ship, keys, subjects);

    var emergency = 0;
    var digests = 0;
    foreach (var pk in keys)
    {
      if (!mesh.Packets.TryGetValue(pk, out var packet))
      {
        continue;
      }

      if (packet.Destination.Feed is { } feed && feed.IsMandatory)
      {
        emergency++;
      }

      if (packet.Topic.Equals(MeshTopics.SpotDigest, StringComparison.Ordinal)
          || (packet.Destination.Feed is { } f
              && f.Value.Equals(MeshFeedId.CommerceSpot.Value, StringComparison.Ordinal)))
      {
        digests++;
      }
    }

    var push = 0;
    if (mesh.TryGetMailbox(person, out var pBox))
    {
      push += pBox.PushedPacketKeys.Count;
    }

    if (mesh.TryGetMailbox(ship, out var sBox))
    {
      push += sBox.PushedPacketKeys.Count;
    }

    var recent = subjects
      .OrderByDescending(s => s.Hour)
      .Select(s => s.Subject)
      .Where(s => !string.IsNullOrWhiteSpace(s))
      .Distinct(StringComparer.Ordinal)
      .Take(8)
      .ToList();

    return new Snapshot(push, keys.Count, emergency, digests, recent);
  }

  /// <summary>Spot-digest packets already in person or ship feed inbox.</summary>
  public static IEnumerable<MeshPacket> SpotDigestsInInbox(MeshState mesh)
  {
    var person = MeshIdentityIds.Person(CampaignWorld.PlayerFlavorId);
    var ship = MeshIdentityIds.Ship(CampaignWorld.PlayerHullName);
    var seen = new HashSet<string>(StringComparer.Ordinal);
    foreach (var owner in new[] { person, ship })
    {
      if (!mesh.FeedInboxes.TryGetValue(owner.Value, out var inbox))
      {
        continue;
      }

      foreach (var pk in inbox)
      {
        if (!seen.Add(pk) || !mesh.Packets.TryGetValue(pk, out var packet))
        {
          continue;
        }

        if (packet.Topic.Equals(MeshTopics.SpotDigest, StringComparison.Ordinal)
            || (packet.Destination.Feed is { } f
                && f.Value.Equals(MeshFeedId.CommerceSpot.Value, StringComparison.Ordinal)))
        {
          yield return packet;
        }
      }
    }
  }

  private static void Collect(
    MeshState mesh,
    MeshIdentityId owner,
    HashSet<string> keys,
    List<(long Hour, string Subject)> subjects)
  {
    if (mesh.TryGetMailbox(owner, out var box))
    {
      foreach (var pk in box.PushedPacketKeys)
      {
        keys.Add(pk);
        if (mesh.Packets.TryGetValue(pk, out var packet))
        {
          subjects.Add((packet.PublishedHour, packet.Subject));
        }
      }
    }

    if (mesh.FeedInboxes.TryGetValue(owner.Value, out var inbox))
    {
      foreach (var pk in inbox)
      {
        keys.Add(pk);
        if (mesh.Packets.TryGetValue(pk, out var packet))
        {
          subjects.Add((packet.PublishedHour, packet.Subject));
        }
      }
    }
  }
}
