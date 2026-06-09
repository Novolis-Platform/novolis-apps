using Novolis.Audio.Live;

namespace LiveStudio;

internal sealed record LiveProgramPreset(
    string Name,
    string Description,
    int Version,
    SwapPolicy SwapPolicy,
    TimeSpan DelayBeforeCompile,
    LiveProgramDefinition Definition);
