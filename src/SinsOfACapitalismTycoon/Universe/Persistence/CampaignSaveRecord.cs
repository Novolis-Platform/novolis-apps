using Novolis.Storage.Abstractions;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>
/// Checkpoint metadata for a campaign. World is rebuilt by replaying the seed to
/// <see cref="HoursDone"/> (deterministic); this document is the durable handle via
/// <c>Novolis.Storage.Json</c>.
/// </summary>
internal sealed class CampaignSaveRecord : IHasId
{
  public const int CurrentSchema = 1;

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

  public bool LocalBoardOnly { get; set; }

  public bool LastTrampWon { get; set; }

  public bool LastTrampLost { get; set; }

  public int DayIndex { get; set; }

  public string HubSystemId { get; set; } = "sol";

  public string SurvivalLine { get; set; } = "";

  public string StandingLine { get; set; } = "";

  public decimal Cash { get; set; }
}
