using LiveStudio.Components.Live;
using Novolis.Audio.Live;

namespace LiveStudio;

internal static class LiveSamplePrograms
{
    public static IReadOnlyList<LiveProgramPreset> CreateShowcasePresets() =>
        LiveDemoCatalog.CreateShowcase().Select(LiveProgramPreset.FromDocument).ToArray();
}
