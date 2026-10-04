using System.Text.Json;

namespace Novolis.Reach.Protocol.Input;

/// <summary>Clipboard notification.</summary>
public sealed record ReachClipboardChanged(string Format);
