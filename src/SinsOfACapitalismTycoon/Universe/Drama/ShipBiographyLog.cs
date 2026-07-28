using Novolis.Economy;
using Novolis.Economy.Logistics;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>Per-hull voyage biography for Spectre narrative (mega always retained).</summary>
internal sealed class ShipBiographyLog
{
  public sealed record Leg(
    int Day,
    FirmId FirmId,
    string ShipName,
    string Origin,
    string Dest,
    string Product,
    decimal Qty,
    TransitProfile Profile,
    decimal WearDelta,
    string Note);

  private readonly List<Leg> _legs = [];
  private readonly Dictionary<FirmId, string> _names = new();

  public IReadOnlyList<Leg> Legs => _legs;

  public void Name(FirmId firm, string name) => _names[firm] = name;

  public void Record(
    int day,
    FirmId firm,
    string origin,
    string dest,
    string product,
    decimal qty,
    TransitProfile profile,
    decimal wearDelta,
    string note)
  {
    var name = _names.GetValueOrDefault(firm, firm.Value.ToString("N")[..8]);
    _legs.Add(new Leg(day, firm, name, origin, dest, product, qty, profile, wearDelta, note));
    if (_legs.Count > 600)
    {
      // Prefer keeping mega (fleet) legs: drop oldest non-mega first.
      var idx = _legs.FindIndex(l => !l.ShipName.Contains("Bulk", StringComparison.OrdinalIgnoreCase));
      if (idx >= 0)
      {
        _legs.RemoveAt(idx);
      }
      else
      {
        _legs.RemoveAt(0);
      }
    }
  }

  public IEnumerable<Leg> ForFirm(FirmId firm) =>
    _legs.Where(l => l.FirmId.Equals(firm)).OrderBy(l => l.Day);
}
