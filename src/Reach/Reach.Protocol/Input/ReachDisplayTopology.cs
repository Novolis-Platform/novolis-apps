using System.Text.Json;

namespace Novolis.Reach.Protocol.Input;

/// <summary>Monitor topology announcement.</summary>
public sealed record ReachDisplayTopology(IReadOnlyList<ReachDisplay> Displays);
