namespace SinsOfACapitalismTycoon.Universe.Mesh;

/// <summary>One ordered transformation of <see cref="MeshState"/>.</summary>
public interface IMeshStep
{
  string Name { get; }
  MeshState Execute(MeshState current);
}

/// <summary>Advances mesh by folding an ordered step list.</summary>
public sealed class MeshEngine(IReadOnlyList<IMeshStep> steps)
{
  public IReadOnlyList<IMeshStep> Steps { get; } = steps ?? throw new ArgumentNullException(nameof(steps));

  public MeshState Advance(MeshState state)
  {
    ArgumentNullException.ThrowIfNull(state);
    return Steps.Aggregate(state, static (current, step) => step.Execute(current));
  }
}
