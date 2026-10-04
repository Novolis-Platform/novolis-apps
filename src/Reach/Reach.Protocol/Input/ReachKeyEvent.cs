using System.Text.Json;

namespace Novolis.Reach.Protocol.Input;

/// <summary>Virtual-key event.</summary>
public sealed record ReachKeyEvent(ushort VirtualKey);
