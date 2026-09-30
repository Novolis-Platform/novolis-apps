using Novolis.Economy;
using Novolis.Economy.Logistics;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>Ship registry book — campaign carriers under the generic <see cref="RegistryBook{T}"/> door.</summary>
internal sealed class ShipRegistry
{
  private readonly RegistryBook<ShipRegistryEntry> _book = new(RegistryKind.Ship);

  public FirmId Underwriter { get; set; }

  /// <summary>Global premium multiplier from underwriter solvency / claims.</summary>
  public decimal ActuarialLoad { get; set; } = 1m;

  public decimal ClaimsPaid { get; set; }

  public IReadOnlyCollection<ShipRegistryEntry> Entries => _book.Entries;

  public void Register(ShipRegistryEntry entry)
  {
    if (entry.RatedLife <= 0m)
    {
      entry.RatedLife = FtlDriveLifePolicy.RatedLifeForHull(entry.HullClass);
    }

    _book.Register(entry);
  }

  public ShipRegistryEntry? TryGet(FirmId firm) =>
    _book.TryGet(firm.Value);

  public bool CanOperate(FirmId firm) =>
    TryGet(firm) is { } e && e.CanOperate;

  /// <summary>Fold underway mileage into life + acute wear; burnout if past rated life.</summary>
  public void ObserveMileage(FirmId firm, decimal lifeUnits, TransitProfile profile)
  {
    if (TryGet(firm) is not { } e || lifeUnits <= 0m)
    {
      return;
    }

    e.LifeUsed += lifeUnits;
    e.DriveWear += lifeUnits;
    ObserveProfile(firm, profile);

    if (e.LifeUsed >= e.RatedLife)
    {
      e.BurnedOut = true;
      e.Suspended = true;
      e.YardCleared = false;
    }
  }

  public void ObserveProfile(FirmId firm, TransitProfile profile)
  {
    if (TryGet(firm) is not { } e)
    {
      return;
    }

    switch (profile)
    {
      case TransitProfile.SlowEconomic:
        e.SlowLegs++;
        break;
      case TransitProfile.PriorityCommercial:
        e.PriorityLegs++;
        break;
      default:
        e.StandardLegs++;
        break;
    }
  }

  public void ObserveLongLane(FirmId firm)
  {
    if (TryGet(firm) is { } e)
    {
      e.LongLaneLegs++;
    }
  }

  public decimal QuoteDailyPremium(ShipRegistryEntry e)
  {
    var basePremium = e.OwnerMaster ? 14m : 32m;
    if (e.HullClass.Contains("Mega", StringComparison.OrdinalIgnoreCase))
    {
      basePremium = 48m;
    }

    return HullRiskQuotes.DailyPremium(
      basePremium,
      e.LifeFraction,
      e.PriorityLegs,
      e.LongLaneLegs,
      ActuarialLoad,
      idleOrSuspended: e.BurnedOut || e.Suspended);
  }

  /// <summary>
  /// Daily premium for the hull's <b>category</b> (owner-master / mega / life / load).
  /// Accrues over calendar time — not per job. Idle/burned bands are inside
  /// <see cref="HullRiskQuotes.DailyPremium"/> via suspended/burned flags.
  /// </summary>
  public decimal QuotePremiumDue(ShipRegistryEntry e, bool operatingInTransit = false)
  {
    _ = operatingInTransit; // retained for call-site compat; billing is category-over-time
    return QuoteDailyPremium(e);
  }

  /// <summary>Obsolete alias — prefer <see cref="QuotePremiumDue"/>.</summary>
  public const decimal IdlePremiumFactor = 1m;

  public decimal QuoteElectiveOverhaul(ShipRegistryEntry e)
  {
    var hull = e.HullClass.Contains("Mega", StringComparison.OrdinalIgnoreCase) ? 1.8m : 1m;
    return HullRiskQuotes.ElectiveOverhaul(e.LifeUsed, hull);
  }

  public decimal QuoteBurnoutOverhaul(ShipRegistryEntry e) =>
    HullRiskQuotes.BurnoutOverhaul(
      e.LifeUsed,
      e.HullClass.Contains("Mega", StringComparison.OrdinalIgnoreCase) ? 1.8m : 1m);

  public decimal QuoteYardService(ShipRegistryEntry e) =>
    e.BurnedOut ? QuoteBurnoutOverhaul(e) : QuoteElectiveOverhaul(e);

  public void ApplyOverhaul(ShipRegistryEntry e)
  {
    e.LifeUsed = 0m;
    e.DriveWear = 0m;
    e.BurnedOut = false;
    e.Suspended = false;
    e.YardCleared = true;
    e.OverhaulCount++;
    e.PriorityLegs = Math.Max(0, e.PriorityLegs / 3);
    e.LongLaneLegs = Math.Max(0, e.LongLaneLegs / 3);
  }

  public void DecayAcuteWear(ShipRegistryEntry e)
  {
    e.DriveWear = Math.Max(0m, e.DriveWear * (1m - FtlDriveLifePolicy.AcuteWearDecayPerDay));
  }
}
