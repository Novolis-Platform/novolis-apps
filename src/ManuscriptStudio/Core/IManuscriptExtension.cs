using Avalonia.Controls;

namespace ManuscriptStudio.Core;

internal interface IManuscriptExtension
{
    string Id { get; }
    string DisplayName { get; }

    Control CreateLeftRail(ManuscriptHostContext host);

    void ConfigureToolbar(StackPanel toolbar, ManuscriptHostContext host);

    string RenderPreviewHtml(ManuscriptHostContext host);

    void OnActivated(ManuscriptHostContext host);

    void OnDeactivated(ManuscriptHostContext host);
}
