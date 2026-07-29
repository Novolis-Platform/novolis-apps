using SinsOfACapitalismTycoon.Universe.Mesh.Kernel;
namespace SinsOfACapitalismTycoon.Universe.Mesh.Sins;

/// <summary>Campaign hour tick + smoke publish helpers.</summary>
internal static class MeshPulse
{
  private static readonly MeshEngine Engine = DefaultMeshPipeline.CreateEngine();

  public static MeshState TickHour(MeshState state, bool assertInvariants = true)
  {
    var next = Engine.Advance(state);
    if (assertInvariants)
    {
      InvariantChecker.AssertAll(next);
    }

    return next;
  }

  /// <summary>
  /// Seed directed Sol→Wolf, identity mail for Calypso, News.General (opt-in),
  /// and Emergency (forced to every co-located mailbox).
  /// </summary>
  public static MeshState SeedSmokePublishes(
    MeshState state,
    MeshIdentityId calypsoIdentity,
    PacketId? directedId = null,
    PacketId? identityId = null,
    PacketId? feedId = null,
    PacketId? emergencyId = null)
  {
    if (!state.Nodes.ContainsKey("sol"))
    {
      return state;
    }

    var sol = MeshNodeId.From("sol");
    var dest = state.Nodes.Values
      .Select(n => n.Id)
      .FirstOrDefault(n => !n.Value.Equals("sol", StringComparison.OrdinalIgnoreCase));
    if (string.IsNullOrEmpty(dest.Value))
    {
      return state;
    }

    if (state.Nodes.ContainsKey("wolf359"))
    {
      dest = MeshNodeId.From("wolf359");
    }

    (state, _) = PublishEngine.PublishPulse(
      state,
      sol,
      MeshAddress.ToPlace(dest),
      priority: 2,
      id: directedId ?? PacketId.From(Guid.Parse("00000000-0000-4000-8000-00000000f001")));

    (state, _) = PublishEngine.PublishPulse(
      state,
      sol,
      MeshAddress.ToIdentity(calypsoIdentity),
      priority: 1,
      id: identityId ?? PacketId.From(Guid.Parse("00000000-0000-4000-8000-00000000f002")));

    (state, _) = PublishEngine.PublishPulse(
      state,
      sol,
      MeshAddress.ToFeed(MeshFeedId.NewsGeneral),
      priority: 1,
      id: feedId ?? PacketId.From(Guid.Parse("00000000-0000-4000-8000-00000000f003")));

    (state, _) = PublishEngine.PublishPulse(
      state,
      sol,
      MeshAddress.ToFeed(MeshFeedId.Emergency),
      priority: 10,
      id: emergencyId ?? PacketId.From(Guid.Parse("00000000-0000-4000-8000-00000000f004")));

    state = FeedEngine.Subscribe(state, calypsoIdentity, MeshFeedId.NewsGeneral);

    state = LaunchEngine.LaunchPending(state);
    state = FeedEngine.Pull(state, calypsoIdentity);
    return state;
  }
}
