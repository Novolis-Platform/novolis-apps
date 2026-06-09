using Novolis.Audio.Live.Protocol.Dto;
using Novolis.Audio.Live.Visuals;

namespace LiveStudio;

internal sealed record LiveStudioState(
    string LauncherStatus,
    string ConnectionStatus,
    string ActivityStatus,
    string CurrentPresetName,
    string? NextPresetName,
    LiveTransportSnapshotDto? Snapshot,
    LiveGraphNode? Graph,
    IReadOnlyList<LiveDiagnosticDto> Diagnostics,
    IReadOnlyList<LiveProgramPreset> Presets,
    string? ErrorMessage = null,
    bool HasFatalLauncherError = false);
