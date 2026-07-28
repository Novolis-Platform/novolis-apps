using Novolis.Economy;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>
/// Meridian-shaped reputation: finished ugly work and on-time Final become future work preference.
/// Score 0–100; high score lowers effective MinMargin slightly (more jobs clear).
/// </summary>
internal sealed class ReputationLedger
{
  private readonly Dictionary<FirmId, decimal> _score = new();
  private int _lastDecayDay = -1;

  public decimal Get(FirmId firm) =>
    _score.TryGetValue(firm, out var s) ? s : 25m;

  public void Bump(FirmId firm, decimal delta, string reason)
  {
    _ = reason;
    var next = Math.Clamp(Get(firm) + delta, 0m, 100m);
    _score[firm] = next;
  }

  public void ObserveKnownResponsive(FirmId firm) => Bump(firm, 12m, "known-responsive");

  public void ObserveStandbyComplete(FirmId firm) => Bump(firm, 8m, "standby-complete");

  public void ObserveFinalDelivery(FirmId firm) => Bump(firm, 1.5m, "final-delivery");

  /// <summary>Weekly soft decay so reputation must be maintained.</summary>
  public void TickDay(int dayIndex)
  {
    if (dayIndex <= 0 || dayIndex % 7 != 0 || dayIndex == _lastDecayDay)
    {
      return;
    }

    _lastDecayDay = dayIndex;
    foreach (var firm in _score.Keys.ToList())
    {
      _score[firm] = Math.Max(0m, _score[firm] - 1.5m);
    }
  }

  /// <summary>High rep → slightly lower MinMargin (more hauls clear the bar).</summary>
  public decimal EffectiveMinMargin(FirmId firm, decimal baseMargin)
  {
    var score = Get(firm);
    return Math.Max(0.05m, baseMargin * (1.05m - score / 500m));
  }

  public IEnumerable<(FirmId Firm, decimal Score)> Ranked() =>
    _score.OrderByDescending(kv => kv.Value).Select(kv => (kv.Key, kv.Value));

  public FirmId? PreferOperable(IEnumerable<FirmId> candidates, Func<FirmId, bool> canOperate)
  {
    var list = candidates
      .Where(canOperate)
      .OrderByDescending(Get)
      .ThenBy(f => f.Value)
      .ToList();
    return list.Count == 0 ? null : list[0];
  }
}
