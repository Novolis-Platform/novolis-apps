using Novolis.Economy;
using Novolis.Economy.Logistics;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>
/// Hull record on the ship registry — drive life, insurance, owner-master standing.
/// Inherits the generic door (<see cref="RegistryRecord.CanAct"/>) and adds FTL wear.
/// </summary>
internal sealed class ShipRegistryEntry : RegistryRecord
{
  public required FirmId FirmId { get; init; }
  public required string HullClass { get; init; }
  public bool OwnerMaster { get; init; } = true;
  public bool Insured { get; set; } = true;
  /// <summary>Acute stress since last overhaul (claims / soft metrics).</summary>
  public decimal DriveWear { get; set; }
  /// <summary>Life mileage consumed on the current drive stack (resets on overhaul).</summary>
  public decimal LifeUsed { get; set; }
  /// <summary>Rated life before guaranteed burnout.</summary>
  public decimal RatedLife { get; set; } = FtlDriveLifePolicy.RatedLifeLight;
  public int OverhaulCount { get; set; }
  public bool BurnedOut { get; set; }
  public decimal PremiumPaid { get; set; }
  public decimal MaintenancePaid { get; set; }
  public decimal ClaimsReceived { get; set; }
  /// <summary>Accrued premium liability not yet settled in cash (wage-style payable).</summary>
  public decimal PremiumPayable { get; set; }
  /// <summary>Consecutive days with unpaid premium payable and no cash settlement.</summary>
  public int PremiumArrearsDays { get; set; }
  public int PriorityLegs { get; set; }
  public int SlowLegs { get; set; }
  public int StandardLegs { get; set; }
  public int LongLaneLegs { get; set; }
  /// <summary>Yard / overhaul cleared after suspension.</summary>
  public bool YardCleared { get; set; }

  public decimal LifeFraction =>
    RatedLife <= 0m ? 1m : Math.Clamp(LifeUsed / RatedLife, 0m, 1.5m);

  public bool OverhaulDue => LifeUsed >= RatedLife * FtlDriveLifePolicy.ElectiveOverhaulFraction;

  /// <summary>Hull door: insured + not suspended/burned-out/revoked.</summary>
  public bool CanOperate => Insured && CanAct && !BurnedOut;

  public override bool CanAct => base.CanAct && !BurnedOut;

  public override RegistryStandingKind Standing =>
    BurnedOut || Revoked ? RegistryStandingKind.Revoked
    : Suspended ? RegistryStandingKind.Suspended
    : !Insured || PremiumArrearsDays > 0 || OverhaulDue ? RegistryStandingKind.Restricted
    : RegistryStandingKind.Operable;

  public override string StandingLabel =>
    BurnedOut ? "burned-out"
    : Suspended ? "suspended"
    : !Insured ? "uninsured"
    : PremiumArrearsDays > 0 ? "arrears"
    : OverhaulDue ? "overhaul-due"
    : OwnerMaster ? "owner-master"
    : "fleet";

  public static ShipRegistryEntry Create(
    FirmId firm,
    string registryName,
    string hullClass,
    bool ownerMaster = true,
    decimal lienPrincipal = 0m) =>
    new()
    {
      SubjectId = firm.Value,
      Kind = RegistryKind.Ship,
      RegistryName = registryName,
      FirmId = firm,
      HullClass = hullClass,
      OwnerMaster = ownerMaster,
      LienPrincipal = lienPrincipal,
    };
}
