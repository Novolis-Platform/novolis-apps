namespace SinsOfACapitalismTycoon.Universe;

/// <summary>
/// Lightweight playtest tags for judging fun (MDA dynamics → aesthetics).
/// Not a telemetry SaaS — session counters for Spectre / unit assertions.
/// </summary>
internal sealed class FunTelemetry
{
  public int FirstAccepts { get; private set; }
  public int EscrowReleases { get; private set; }
  public int MeshUnlocks { get; private set; }
  public int SoftFailNearMisses { get; private set; }
  public int SoftFailRecoveries { get; private set; }
  public int SoftFailsRaised { get; private set; }
  public int ReputationLifts { get; private set; }

  public void NoteFirstAccept() => FirstAccepts++;

  public void NoteEscrowRelease() => EscrowReleases++;

  public void NoteMeshUnlock() => MeshUnlocks++;

  public void NoteSoftFailNearMiss() => SoftFailNearMisses++;

  public void NoteSoftFailRaised() => SoftFailsRaised++;

  public void NoteSoftFailRecovery() => SoftFailRecoveries++;

  public void NoteReputationLift() => ReputationLifts++;

  public string SummaryLine() =>
    $"fun · accept {FirstAccepts} · payday {EscrowReleases} · mesh {MeshUnlocks} · near-miss {SoftFailNearMisses} · recover {SoftFailRecoveries} · softfail {SoftFailsRaised} · rep {ReputationLifts}";
}
