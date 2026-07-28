namespace SinsOfACapitalismTycoon.Universe.Mesh;

/// <summary>
/// Timing and capacity knobs. PulseLyPerHour ≈ 20× tramp cruise
/// (<c>CruiseDaysPerLy = 1.3</c> ⇒ tramp ≈ 1/(1.3×24) ly/h).
/// </summary>
public sealed record MeshPolicy(
  double PulseLyPerHour = 0.641025641025641,
  double BulkLyPerHour = 0.03205128205128205,
  int DefaultPulseBandwidthPerHour = 8,
  /// <summary>Deterministic loss: lose drone when (hash % Period) == 0; 0 disables.</summary>
  int LossEveryNth = 0,
  int MaxPendingPerHub = 256);
