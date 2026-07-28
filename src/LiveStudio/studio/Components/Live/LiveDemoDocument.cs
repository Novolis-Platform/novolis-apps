using Novolis.Audio.Live;

namespace LiveStudio.Components.Live;

/// <summary>
/// A live demo document: editable source is the source of truth for the studio.
/// Ready to move into Novolis.Avalonia.Live.
/// </summary>
public sealed record LiveDemoDocument(
    string Id,
    string Title,
    string Description,
    string Source,
    SwapPolicy SwapPolicy,
    TimeSpan DelayBeforeCompile);
