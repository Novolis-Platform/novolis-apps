using Novolis.Economy;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>Issued competence / permit (Priority freight, passenger, salvage, …).</summary>
internal sealed class LicenseRegistryEntry : RegistryRecord
{
  public required string Scope { get; init; }

  public int IssuedDay { get; init; }

  public int? ExpiresDay { get; set; }

  /// <summary>Optional holder (hull firm or person) this license is bonded to.</summary>
  public Guid? HolderSubjectId { get; init; }

  public bool Expired(int dayIndex) =>
    ExpiresDay is { } exp && dayIndex >= exp;

  public override bool CanAct => base.CanAct;

  public bool CanActOn(int dayIndex) => base.CanAct && !Expired(dayIndex);

  public override string StandingLabel =>
    Revoked ? "revoked"
    : Suspended ? "suspended"
    : ExpiresDay is not null ? "term"
    : "licensed";

  public static LicenseRegistryEntry Create(
    Guid licenseId,
    string registryName,
    string scope,
    int issuedDay = 0,
    Guid? holderSubjectId = null,
    int? expiresDay = null) =>
    new()
    {
      SubjectId = licenseId,
      Kind = RegistryKind.License,
      RegistryName = registryName,
      Scope = scope,
      IssuedDay = issuedDay,
      ExpiresDay = expiresDay,
      HolderSubjectId = holderSubjectId,
    };

  /// <summary>Deterministic license id from holder + scope (avoids Guid-prefix collisions).</summary>
  public static Guid IdFor(FirmId holder, string scope)
  {
    var bytes = holder.Value.ToByteArray();
    var mix = System.HashCode.Combine(scope, holder.Value);
    bytes[0] ^= (byte)(mix & 0xFF);
    bytes[1] ^= (byte)((mix >> 8) & 0xFF);
    bytes[2] ^= (byte)((mix >> 16) & 0xFF);
    bytes[3] ^= 0x5A;
    return new Guid(bytes);
  }
}
