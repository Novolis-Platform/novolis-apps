namespace SinsOfACapitalismTycoon.Universe.Mesh;

/// <summary>
/// Timing and capacity knobs. PulseLyPerHour ≈ 20× tramp cruise
/// (<c>CruiseDaysPerLy = 1.3</c> ⇒ tramp ≈ 1/(1.3×24) ly/h).
/// </summary>
public sealed record MeshPolicy(
  double PulseLyPerHour = 0.641025641025641,
  double BulkLyPerHour = 0.03205128205128205,
  int DefaultPulseBandwidthPerHour = 8,
  /// <summary>Deterministic loss: lose on arrival when (hash(packet) % Period) == 0; 0 disables.</summary>
  int LossEveryNth = 0,
  /// <summary>Cap losses per packet so retries can converge (0 = unlimited).</summary>
  int MaxLossesPerPacket = 1,
  int MaxPendingPerHub = 256);
