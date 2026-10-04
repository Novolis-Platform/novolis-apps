using System.Text.Json;

namespace Novolis.Reach.Protocol.Media;

/// <summary>Requests an intra frame.</summary>
public sealed record ReachRequestKeyFrame(long LastSequence);
