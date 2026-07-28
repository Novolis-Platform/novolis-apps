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
}

/// <summary>Issued competence / permit (Priority freight, passenger, salvage, …).</summary>
internal sealed class LicenseRegistryEntry : RegistryRecord
{
  public required string Scope { get; init; }

  public int IssuedDay { get; init; }

  public int? ExpiresDay { get; set; }

  public bool Expired(int dayIndex) =>
    ExpiresDay is { } exp && dayIndex >= exp;

  public override bool CanAct => base.CanAct && ExpiresDay is null;

  public bool CanActOn(int dayIndex) => base.CanAct && !Expired(dayIndex);

  public override string StandingLabel =>
    Revoked ? "revoked"
    : Suspended ? "suspended"
    : ExpiresDay is not null ? "term"
    : "licensed";
}
