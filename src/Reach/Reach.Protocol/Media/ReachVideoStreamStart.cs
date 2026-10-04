using System.Text.Json;

namespace Novolis.Reach.Protocol.Media;

/// <summary>Starts a video stream.</summary>
public sealed record ReachVideoStreamStart(string Codec, int Width, int Height, int FramesPerSecond);
