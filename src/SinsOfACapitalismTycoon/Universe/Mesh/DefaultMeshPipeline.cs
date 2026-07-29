namespace SinsOfACapitalismTycoon.Universe.Mesh;

/// <summary>SPEC ordered hour pipeline.</summary>
public static class DefaultMeshPipeline
{
  public static MeshEngine CreateEngine() => new(
  [
    new NamedStep("DroneTick", DroneTickEngine.Tick),
    new NamedStep("FloodDispatch", FloodEngine.Dispatch),
    new NamedStep("LaunchPending", LaunchEngine.LaunchPending),
    new NamedStep("FeedPullAll", FeedEngine.PullAll),
    new NamedStep("TtlExpire", TtlEngine.Expire),
    new NamedStep("HourAdvance", HourAdvance),
  ]);

  public static MeshState Advance(MeshState state) => CreateEngine().Advance(state);

  private static MeshState HourAdvance(MeshState state) =>
    state with
    {
      HourIndex = state.HourIndex + 1,
      BandwidthUsedThisHour = state.BandwidthUsedThisHour.Clear(),
    };

  private sealed class NamedStep(string name, Func<MeshState, MeshState> exec) : IMeshStep
  {
    public string Name { get; } = name;
    public MeshState Execute(MeshState current) => exec(current);
  }
}
