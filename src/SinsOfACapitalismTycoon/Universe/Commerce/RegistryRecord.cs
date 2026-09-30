using Novolis.Economy;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>
/// Generic registry record: identity, standing, liens, public name.
/// Ship / Firm / License specialize; the door is always <see cref="CanAct"/>.
/// </summary>
internal abstract class RegistryRecord
{
  /// <summary>Stable subject id (hull firm, legal firm, or license guid).</summary>
  public required Guid SubjectId { get; init; }

  public required string RegistryName { get; init; }

  public required RegistryKind Kind { get; init; }

  public bool Suspended { get; set; }

  public bool Revoked { get; set; }

  /// <summary>Debt that follows the registered subject (hull / firm / bonded license).</summary>
  public decimal LienPrincipal { get; set; }

  /// <summary>True when ports / counterparties may treat this subject as operable.</summary>
  public virtual bool CanAct => !Suspended && !Revoked;

  public virtual RegistryStandingKind Standing =>
    Revoked ? RegistryStandingKind.Revoked
    : Suspended ? RegistryStandingKind.Suspended
    : RegistryStandingKind.Operable;

  public virtual string StandingLabel => Standing.ToString().ToLowerInvariant();
}
