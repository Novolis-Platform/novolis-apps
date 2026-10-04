using System.Text.Json;

namespace Novolis.Reach.Protocol.Transfer;

/// <summary>Completes a file transfer.</summary>
public sealed record ReachFileComplete(Guid TransferId, bool Succeeded, string? Error);
