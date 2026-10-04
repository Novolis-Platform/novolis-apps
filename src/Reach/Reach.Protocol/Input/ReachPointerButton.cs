using System.Text.Json;

namespace Novolis.Reach.Protocol.Input;

/// <summary>Pointer button event.</summary>
public sealed record ReachPointerButton(string Button, bool IsDown, int ClickCount = 1);
