using Novolis.Economy;
using Novolis.Economy.Logistics;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>Thread-safe queue of player intents drained by <see cref="PlayerTrampAgent"/>.</summary>
internal sealed class PlayerOrderQueue
{
  private readonly object _gate = new();
  private readonly Queue<PlayerOrder> _q = new();

  public int Count
  {
    get
    {
      lock (_gate)
      {
        return _q.Count;
      }
    }
  }

  public void Enqueue(PlayerOrder order)
  {
    lock (_gate)
    {
      _q.Enqueue(order);
    }
  }

  public bool TryDequeue(out PlayerOrder order)
  {
    lock (_gate)
    {
      if (_q.Count == 0)
      {
        order = null!;
        return false;
      }

      order = _q.Dequeue();
      return true;
    }
  }

  public void Clear()
  {
    lock (_gate)
    {
      _q.Clear();
    }
  }
}
