using LiveStudio.Components.Live;
using Novolis.Audio.Live;

namespace LiveStudio;

/// <summary>App-facing alias for a showcase demo (editable source).</summary>
internal sealed record LiveProgramPreset(
    string Name,
    string Description,
    SwapPolicy SwapPolicy,
    TimeSpan DelayBeforeCompile,
    string Source)
{
    public static LiveProgramPreset FromDocument(LiveDemoDocument doc) =>
        new(doc.Title, doc.Description, doc.SwapPolicy, doc.DelayBeforeCompile, doc.Source);
}
