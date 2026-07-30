using Novolis.Economy.Simulation;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>
/// Advances over <see cref="EconomyState.Events"/> so pulses do not re-scan a fixed TakeLast window.
/// </summary>
internal sealed class SimEventCursor
{
  private int _index;

  public int Index => _index;

  /// <summary>Events appended since the last <see cref="AdvanceToEnd"/> / consume.</summary>
  public IEnumerable<object> Since(EconomySimulation sim)
  {
    var events = sim.State.Events;
    for (var i = _index; i < events.Count; i++)
    {
      yield return events[i];
    }
  }

  /// <summary>Mark all current events as seen (call after processing a pulse slice).</summary>
  public void AdvanceToEnd(EconomySimulation sim) =>
    _index = sim.State.Events.Count;

  /// <summary>Reset when rebuilding a session from seed (fresh event log).</summary>
  public void Reset() => _index = 0;
}
