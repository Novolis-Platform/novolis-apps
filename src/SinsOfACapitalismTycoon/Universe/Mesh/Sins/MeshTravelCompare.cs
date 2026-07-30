using Novolis.Simulation.Mesh;
namespace SinsOfACapitalismTycoon.Universe.Mesh.Sins;

/// <summary>
/// Pulse (FTL) vs tramp hull travel — pulse defaults to ≈20× tramp cruise.
/// </summary>
internal static class MeshTravelCompare
{
  /// <summary>Tramp ly/h implied by policy: <c>PulseLyPerHour / 20</c>.</summary>
  public static double TrampLyPerHour(MeshPolicy policy) => policy.PulseLyPerHour / 20.0;

  public static int PathPulseHours(MeshState state, MeshNodeId from, MeshNodeId to)
  {
    var path = MeshPathfinder.FindPath(state, from, to);
    if (path is null || path.Value.Length < 2)
    {
      return int.MaxValue;
    }

    var hours = 0;
    for (var i = 0; i < path.Value.Length - 1; i++)
    {
      hours += MeshPathfinder.TravelHours(state, path.Value[i], path.Value[i + 1], MeshTrafficLayer.Pulse);
    }

    return hours;
  }

  public static int PathTrampHours(MeshState state, MeshNodeId from, MeshNodeId to)
  {
    var path = MeshPathfinder.FindPath(state, from, to);
    if (path is null || path.Value.Length < 2)
    {
      return int.MaxValue;
    }

    var trampLyH = TrampLyPerHour(state.Policy);
    var hours = 0;
    for (var i = 0; i < path.Value.Length - 1; i++)
    {
      var a = path.Value[i];
      var b = path.Value[i + 1];
      double ly = 0;
      foreach (var e in state.Edges)
      {
        if (e.From.Equals(a) && e.To.Equals(b))
        {
          ly = e.DistanceLy;
          break;
        }
      }

      hours += Math.Max(1, (int)Math.Ceiling(ly / trampLyH));
    }

    return hours;
  }
}
