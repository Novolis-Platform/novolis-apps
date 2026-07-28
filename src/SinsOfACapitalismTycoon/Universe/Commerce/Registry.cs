using Novolis.Economy;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>Which ledger this registry record belongs to (CCA-shaped OS).</summary>
internal enum RegistryKind : byte
{
  Ship = 0,
  Firm = 1,
  License = 2,
  Vehicle = 3,
}

/// <summary>Coarse standing shared by every registry door.</summary>
internal enum RegistryStandingKind : byte
{
  Operable = 0,
  Restricted = 1,
  Suspended = 2,
  Revoked = 3,
}

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

/// <summary>Typed registry book — one door per kind (ship, firm, license, …).</summary>
internal sealed class RegistryBook<T>
  where T : RegistryRecord
{
  private readonly Dictionary<Guid, T> _byId = new();

  public RegistryKind Kind { get; }

  public RegistryBook(RegistryKind kind) => Kind = kind;

  public IReadOnlyCollection<T> Entries => _byId.Values;

  public int Count => _byId.Count;

  public void Register(T entry)
  {
    if (entry.Kind != Kind)
    {
      throw new InvalidOperationException(
        $"Cannot register {entry.Kind} into {Kind} book.");
    }

    _byId[entry.SubjectId] = entry;
  }

  public T? TryGet(Guid subjectId) =>
    _byId.TryGetValue(subjectId, out var e) ? e : null;

  public bool CanAct(Guid subjectId) =>
    TryGet(subjectId) is { } e && e.CanAct;

  public void AttachLien(Guid subjectId, decimal amount)
  {
    if (TryGet(subjectId) is { } e && amount > 0m)
    {
      e.LienPrincipal += amount;
    }
  }
}

/// <summary>
/// Campaign desk: ship / firm / license books under one CCA-shaped roof.
/// Spectre and pulses still talk mostly to <see cref="Ships"/>; firms and licenses are first-class doors.
/// </summary>
internal sealed class CampaignRegistryDesk
{
  public ShipRegistry Ships { get; } = new();

  public RegistryBook<FirmRegistryEntry> Firms { get; } = new(RegistryKind.Firm);

  public RegistryBook<LicenseRegistryEntry> Licenses { get; } = new(RegistryKind.License);

  /// <summary>Backward-compatible alias used across pulses and Spectre.</summary>
  public ShipRegistry Registry => Ships;

  public IEnumerable<RegistryRecord> AllRecords()
  {
    foreach (var s in Ships.Entries)
    {
      yield return s;
    }

    foreach (var f in Firms.Entries)
    {
      yield return f;
    }

    foreach (var l in Licenses.Entries)
    {
      yield return l;
    }
  }

  public bool CanAct(RegistryKind kind, Guid subjectId) =>
    kind switch
    {
      RegistryKind.Ship => Ships.CanOperate(FirmId.From(subjectId)),
      RegistryKind.Firm => Firms.CanAct(subjectId),
      RegistryKind.License => Licenses.CanAct(subjectId),
      _ => false,
    };
}

/// <summary>Legal-person standing (Mining, Industry, Station, …).</summary>
internal sealed class FirmRegistryEntry : RegistryRecord
{
  public required FirmId FirmId { get; init; }

  public bool Blacklisted
  {
    get => Revoked;
    set => Revoked = value;
  }

  public override string StandingLabel =>
    Blacklisted ? "blacklisted"
    : Suspended ? "suspended"
    : LienPrincipal > 0m ? "encumbered"
    : "solvent";

  public static FirmRegistryEntry Create(FirmId firm, string registryName) =>
    new()
    {
      SubjectId = firm.Value,
      Kind = RegistryKind.Firm,
      RegistryName = registryName,
      FirmId = firm,
    };
}

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
