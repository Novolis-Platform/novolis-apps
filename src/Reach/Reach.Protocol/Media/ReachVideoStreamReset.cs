using System.Text.Json;

namespace Novolis.Reach.Protocol.Media;

/// <summary>Resets a video stream decoder.</summary>
public sealed record ReachVideoStreamReset(long Sequence);
