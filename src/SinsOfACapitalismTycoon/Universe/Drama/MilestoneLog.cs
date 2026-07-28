namespace SinsOfACapitalismTycoon.Universe;

/// <summary>Greppable campaign story beats (<c>MILESTONE:</c> lines in Spectre).</summary>
internal sealed class MilestoneLog
{
  private readonly List<Entry> _entries = [];
  private readonly HashSet<string> _once = new(StringComparer.Ordinal);
  private int _announced;

  public sealed record Entry(int Day, string Kind, string Detail);

  public IReadOnlyList<Entry> Entries => _entries;

  public void Add(int day, string kind, string detail)
  {
    _entries.Add(new Entry(day, kind, detail));
    if (_entries.Count > 400)
    {
      _entries.RemoveRange(0, _entries.Count - 400);
      _announced = Math.Min(_announced, _entries.Count);
    }
  }

  public void AddOnce(int day, string kind, string detail)
  {
    var key = kind + "|" + detail;
    if (!_once.Add(key))
    {
      return;
    }

    Add(day, kind, detail);
  }

  public int CountKind(string kind) =>
    _entries.Count(e => e.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase));

  /// <summary>New entries since last call — for live radio tickers during a run.</summary>
  public IReadOnlyList<Entry> DrainNew()
  {
    if (_announced >= _entries.Count)
    {
      return Array.Empty<Entry>();
    }

    var slice = _entries.Skip(_announced).ToList();
    _announced = _entries.Count;
    return slice;
  }
}
