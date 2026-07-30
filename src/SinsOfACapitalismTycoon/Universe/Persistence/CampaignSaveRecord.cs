using Novolis.Storage.Abstractions;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>
/// Deterministic replay checkpoint — not a world dump. Load rebuilds the campaign by
/// replaying <see cref="Seed"/> for <see cref="HoursDone"/> hours, then verifies
/// <see cref="SimHash"/> / day / ops cash against this record.
/// </summary>
internal sealed class CampaignSaveRecord : IHasId
{
  public const int CurrentSchema = 2;

  public Guid Id { get; set; }

  public int SchemaVersion { get; set; } = CurrentSchema;

  public string Label { get; set; } = "";

  public DateTimeOffset SavedUtc { get; set; }

  public ulong Seed { get; set; }

  public long HorizonHours { get; set; }

  public long HoursDone { get; set; }

  public bool Drama { get; set; } = true;

  public bool LastTramp { get; set; }

  public bool Autopilot { get; set; }

  public bool Player { get; set; } = true;

  public bool DockBoardOnly { get; set; }

  /// <summary>True after first Calypso escrow release — Mesh board filter available.</summary>
  public bool MeshBoardUnlocked { get; set; }

  /// <summary>Legacy save field — maps to <see cref="DockBoardOnly"/>.</summary>
  public bool LocalBoardOnly
  {
    get => DockBoardOnly;
    set => DockBoardOnly = value;
  }

  public bool LastTrampWon { get; set; }

  public bool LastTrampLost { get; set; }

  public int DayIndex { get; set; }

  public string SystemId { get; set; } = "sol";

  /// <summary>Legacy save field — maps to <see cref="SystemId"/>.</summary>
  public string HubSystemId
  {
    get => SystemId;
    set => SystemId = value;
  }

  public string SurvivalLine { get; set; } = "";

  public string StandingLine { get; set; } = "";

  /// <summary>Display alias for <see cref="OpsCash"/>.</summary>
  public decimal Cash
  {
    get => OpsCash;
    set => OpsCash = value;
  }

  /// <summary>Tramp firm ops cash at save — integrity check after warm replay.</summary>
  public decimal OpsCash { get; set; }

  /// <summary><see cref="Novolis.Economy.Simulation.SimulationState.Hash"/> at save.</summary>
  public ulong SimHash { get; set; }

  /// <summary>True when this record carries integrity fields (schema ≥ 2 or hash set).</summary>
  public bool HasIntegrity => SchemaVersion >= 2 || SimHash != 0;
}
