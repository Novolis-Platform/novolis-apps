using Novolis.Audio.Live;
using Novolis.Avalonia.Live;

namespace LiveStudio;

internal static class LiveSamplePrograms
{
    public static IReadOnlyList<LiveProgramPreset> CreateShowcasePresets() =>
        LiveDemoCatalog.CreateShowcase().Select(LiveProgramPreset.FromDocument).ToArray();
}
