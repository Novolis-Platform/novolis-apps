using Avalonia.Controls;

namespace ManuscriptStudio.Core;

internal interface IManuscriptExtension
{
    string Id { get; }
    string DisplayName { get; }

    Control CreateLeftRail(ManuscriptHostContext host);

    void ConfigureNavigationBar(StackPanel bar, ManuscriptHostContext host);

    void ConfigureEditorBar(StackPanel bar, ManuscriptHostContext host);

    void ConfigurePreviewBar(StackPanel bar, ManuscriptHostContext host);

    IReadOnlyList<RightRailViewDescriptor> GetRightRailViews();

    string DefaultRightRailViewId { get; }

    Control CreateRightRail(ManuscriptHostContext host, string viewId);

    void OnRightRailViewChanged(ManuscriptHostContext host, string viewId);

    void OnActivated(ManuscriptHostContext host);

    void OnDeactivated(ManuscriptHostContext host);
}
