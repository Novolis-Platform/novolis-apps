using System.Collections.Immutable;

namespace SinsOfACapitalismTycoon.Universe.Mesh;

/// <summary>Campaign hour tick + smoke publish helpers.</summary>
public static class MeshPulse
{
  public static MeshState TickHour(MeshState state)
  {
    var next = DefaultMeshPipeline.Advance(state);
    InvariantChecker.AssertAll(next);
    return next;
  }

  /// <summary>
  /// Seed directed Sol→first non-Sol hub + identity flood for Calypso.
  /// Call once after mesh + identities are registered.
  /// </summary>
  public static MeshState SeedSmokePublishes(
    MeshState state,
    MeshIdentityId calypsoIdentity,
    PacketId? directedId = null,
    PacketId? floodId = null)
  {
    if (!state.Hubs.ContainsKey("sol"))
    {
      return state;
    }

    var sol = MeshHubId.From("sol");
    var dest = state.Hubs.Values
      .Select(h => h.Id)
      .FirstOrDefault(h => !h.Value.Equals("sol", StringComparison.OrdinalIgnoreCase));
    if (dest.Value is null)
    {
      return state;
    }

    // Prefer wolf359 if present
    if (state.Hubs.ContainsKey("wolf359"))
    {
      dest = MeshHubId.From("wolf359");
    }

    (state, _) = PublishEngine.PublishPulse(
      state,
      sol,
      MeshAddress.ToPlace(dest),
      priority: 2,
      id: directedId ?? PacketId.From(Guid.Parse("00000000-0000-4000-8000-00000000m001")));

    (state, _) = PublishEngine.PublishPulse(
      state,
      sol,
      MeshAddress.ToIdentity(calypsoIdentity),
      priority: 1,
      id: floodId ?? PacketId.From(Guid.Parse("00000000-0000-4000-8000-00000000m002")));

    // Drain pending launches for hour 0 so smoke is in-flight immediately
    state = LaunchEngine.LaunchPending(state);
    return state;
  }
}
