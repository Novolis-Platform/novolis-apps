#if false
// Superseded by Novolis.Testing.Timeline.TimelineEvent.
namespace Reach.UiTests;

internal sealed record ReachTimelineEvent(
    int Sequence,
    DateTimeOffset CapturedAtUtc,
    string Name,
    string Status,
    string? Detail,
    IReadOnlyDictionary<string, object?>? Metadata);
#endif
