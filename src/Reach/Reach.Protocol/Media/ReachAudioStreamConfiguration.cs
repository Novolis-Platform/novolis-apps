using System.Text.Json;

namespace Novolis.Reach.Protocol.Media;

/// <summary>Changes audio stream configuration.</summary>
public sealed record ReachAudioStreamConfiguration(string Codec, int SampleRate, int Channels);
