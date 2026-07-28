using Novolis.Economy;
using Novolis.Economy.Logistics;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>
/// Registry standing for a commercial hull — the "door" from owner-master memoir fiction.
/// Drive life is mileage: speed × mass × distance burn the FTL; overhaul or guaranteed burnout.
/// Quote math lives in <see cref="HullRiskQuotes"/> / <see cref="FtlDriveLifePolicy"/>.
/// </summary>
internal sealed class ShipRegistryEntry
{
  public required FirmId FirmId { get; init; }
  public required string RegistryName { get; init; }
  public required string HullClass { get; init; }
  public bool OwnerMaster { get; init; } = true;
  public bool Insured { get; set; } = true;
  public bool Suspended { get; set; }
  /// <summary>Acute stress since last overhaul (claims / soft metrics).</summary>
  public decimal DriveWear { get; set; }
  /// <summary>Life mileage consumed on the current drive stack (resets on overhaul).</summary>
  public decimal LifeUsed { get; set; }
  /// <summary>Rated life before guaranteed burnout (hull-class). Overhaul before this — or burn out.</summary>
  public decimal RatedLife { get; set; } = FtlDriveLifePolicy.RatedLifeLight;
  public int OverhaulCount { get; set; }
  public bool BurnedOut { get; set; }
  public decimal PremiumPaid { get; set; }
  public decimal MaintenancePaid { get; set; }
  public decimal ClaimsReceived { get; set; }
  /// <summary>Consecutive days premium unpaid while still marked insured (grace before uninsured).</summary>
  public int PremiumArrearsDays { get; set; }
  public int PriorityLegs { get; set; }
  public int SlowLegs { get; set; }
  public int StandardLegs { get; set; }
  public int LongLaneLegs { get; set; }
  /// <summary>Yard / overhaul cleared after suspension.</summary>
  public bool YardCleared { get; set; }
  /// <summary>Debt that follows the hull (venture loan / escrow clawback).</summary>
  public decimal LienPrincipal { get; set; }

  public decimal LifeFraction =>
    RatedLife <= 0m ? 1m : Math.Clamp(LifeUsed / RatedLife, 0m, 1.5m);

  public bool OverhaulDue => LifeUsed >= RatedLife * FtlDriveLifePolicy.ElectiveOverhaulFraction;

  public bool CanOperate => Insured && !Suspended && !BurnedOut;

  public string StandingLabel =>
    BurnedOut ? "burned-out"
    : Suspended ? "suspended"
    : !Insured ? "uninsured"
    : PremiumArrearsDays > 0 ? "arrears"
    : OverhaulDue ? "overhaul-due"
    : OwnerMaster ? "owner-master"
    : "fleet";
}

/// <summary>Common commercial registry for campaign carriers (app-level commerce law).</summary>
internal sealed class ShipRegistry
{
  private readonly Dictionary<FirmId, ShipRegistryEntry> _byFirm = new();

  public FirmId Underwriter { get; init; }

  /// <summary>Global premium multiplier from underwriter solvency / claims.</summary>
  public decimal ActuarialLoad { get; set; } = 1m;

  public decimal ClaimsPaid { get; set; }

  public IReadOnlyCollection<ShipRegistryEntry> Entries => _byFirm.Values;

  public void Register(ShipRegistryEntry entry)
  {
    if (entry.RatedLife <= 0m)
    {
      entry.RatedLife = FtlDriveLifePolicy.RatedLifeForHull(entry.HullClass);
    }

    _byFirm[entry.FirmId] = entry;
  }

  public ShipRegistryEntry? TryGet(FirmId firm) =>
    _byFirm.TryGetValue(firm, out var e) ? e : null;

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

  /// <summary>
  /// Daily insurance: host base premium + <see cref="HullRiskQuotes.DailyPremium"/>.
  /// </summary>
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

  /// <summary>Elective overhaul (before burnout) — cheaper scheduled stack swap.</summary>
  public decimal QuoteElectiveOverhaul(ShipRegistryEntry e)
  {
    var hull = e.HullClass.Contains("Mega", StringComparison.OrdinalIgnoreCase) ? 1.8m : 1m;
    return HullRiskQuotes.ElectiveOverhaul(e.LifeUsed, hull);
  }

  /// <summary>Forced overhaul after guaranteed burnout — yard emergency.</summary>
  public decimal QuoteBurnoutOverhaul(ShipRegistryEntry e) =>
    HullRiskQuotes.BurnoutOverhaul(
      e.LifeUsed,
      e.HullClass.Contains("Mega", StringComparison.OrdinalIgnoreCase) ? 1.8m : 1m);

  /// <summary>Legacy alias: soft yard bill ≈ elective when not burned out.</summary>
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
    // Fresh stack: damp profile history so premiums don't stay Priority-poisoned forever.
    e.PriorityLegs = Math.Max(0, e.PriorityLegs / 3);
    e.LongLaneLegs = Math.Max(0, e.LongLaneLegs / 3);
  }

  public void DecayAcuteWear(ShipRegistryEntry e)
  {
    e.DriveWear = Math.Max(0m, e.DriveWear * (1m - FtlDriveLifePolicy.AcuteWearDecayPerDay));
  }
}
