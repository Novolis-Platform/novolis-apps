using Avalonia.Controls;
using Novolis.Avalonia.Studio;

namespace ManuscriptStudio.Core;

internal sealed class ManuscriptHostContext
{
    public required EditorSession Session { get; init; }
    public required ManuscriptSettingsStore Settings { get; init; }
    public required StudioFeedback Feedback { get; init; }
    public required Action RequestPreviewRefresh { get; init; }
    public required Func<string> GetEditorText { get; init; }
    public required Action<string> SetEditorText { get; init; }
    public required Func<Task<string?>> PickFolderAsync { get; init; }
    public required Func<Task<string?>> PickExportFolderAsync { get; init; }
    public required Window MainWindow { get; init; }
    public required Action UpdateStatus { get; init; }
    public required Action UpdateDirtyIndicator { get; init; }
    public required Action RefreshRightRail { get; init; }
    public required Func<string> GetRightRailViewId { get; init; }
    public required Action<string> SetRightRailViewId { get; init; }
}
