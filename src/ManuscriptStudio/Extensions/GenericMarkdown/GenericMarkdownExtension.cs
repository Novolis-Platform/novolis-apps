using Avalonia.Controls;
using ManuscriptStudio.Core;
using Novolis.Avalonia.Studio;

namespace ManuscriptStudio.Extensions.GenericMarkdown;

internal sealed class GenericMarkdownExtension : IManuscriptExtension
{
    public const string ExtensionId = "generic-markdown";

    private TreeView? _fileTree;
    private ManuscriptHostContext? _host;
    private HtmlPreviewPane? _previewPanel;

    public string Id => ExtensionId;
    public string DisplayName => "Generic Markdown";
    public string DefaultRightRailViewId => RightRailViewDescriptor.Preview.Id;

    public Control CreateLeftRail(ManuscriptHostContext host)
    {
        _host = host;
        _fileTree = new TreeView { Margin = new Avalonia.Thickness(4) };
        _fileTree.SelectionChanged += OnTreeSelectionChanged;
        RebuildTree();
        return new ScrollViewer
        {
            Content = _fileTree,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
    }

    public void ConfigureToolbar(StackPanel toolbar, ManuscriptHostContext host)
    {
        _host = host;
        var openBtn = ToolbarButton("Open folder…");
        openBtn.Click += async (_, _) =>
        {
            var path = await host.PickFolderAsync();
            if (path is not null)
                OpenWorkspace(path);
        };
        toolbar.Children.Add(openBtn);
    }

    public IReadOnlyList<RightRailViewDescriptor> GetRightRailViews() =>
        [RightRailViewDescriptor.Preview];

    public Control CreateRightRail(ManuscriptHostContext host, string viewId)
    {
        _host = host;
        _previewPanel = CreatePreviewPane(host);
        return _previewPanel;
    }

    public void OnRightRailViewChanged(ManuscriptHostContext host, string viewId)
    {
        _host = host;
        RefreshPreview(host);
    }

    public void OnActivated(ManuscriptHostContext host)
    {
        _host = host;
        RebuildTree();
    }

    public void OnDeactivated(ManuscriptHostContext host) => _host = null;

    private HtmlPreviewPane CreatePreviewPane(ManuscriptHostContext host)
    {
        var pane = new HtmlPreviewPane
        {
            ZoomScale = host.PreviewZoomScale,
            PreviewTheme = host.PreviewTheme,
        };
        pane.ZoomScaleChanged += (_, scale) => _host?.OnPreviewZoomScaleChanged(scale);
        return pane;
    }

    private void RefreshPreview(ManuscriptHostContext host)
    {
        if (_previewPanel is null)
            return;

        _previewPanel.PreviewTheme = host.PreviewTheme;
        _previewPanel.ZoomScale = host.PreviewZoomScale;
        _previewPanel.DocumentBodyHtml = MarkdownStudioHtml.BodyFromMarkdown(host.GetEditorText());
    }

    private async void OpenWorkspace(string path)
    {
        if (_host is null)
            return;

        try
        {
            _host.Session.OpenWorkspace(path);
            _host.Settings.Settings.LastWorkspaceRoot = path;
            _host.Settings.Save();
            RebuildTree();
            _host.SetEditorText(_host.Session.EditorText);
            _host.RefreshRightRail();
            _host.UpdateDirtyIndicator();
            _host.UpdateStatus();
            _host.Feedback.Flash($"Opened {path}");
        }
        catch (Exception ex)
        {
            _host.Feedback.FlashError(ex.Message);
        }
    }

    private void RebuildTree()
    {
        if (_fileTree is null || _host?.Session.WorkspaceRoot is null)
        {
            if (_fileTree is not null)
                _fileTree.Items.Clear();
            return;
        }

        var tree = MarkdownFileTreeBuilder.Build(_host.Session.WorkspaceRoot);
        _fileTree.Items.Clear();
        foreach (var node in tree)
            _fileTree.Items.Add(CreateTreeItem(node));
    }

    private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_host is null || _fileTree?.SelectedItem is not TreeViewItem { Tag: MarkdownTreeNode node } || !node.IsFile || node.FilePath is null)
            return;

        if (_host.Session.IsDirty)
            _host.Feedback.FlashWarning("Unsaved changes were discarded.");

        try
        {
            _host.Session.SelectFile(node.FilePath);
            _host.SetEditorText(_host.Session.EditorText);
            _host.RefreshRightRail();
            _host.UpdateDirtyIndicator();
            _host.UpdateStatus();
        }
        catch (Exception ex)
        {
            _host.Feedback.FlashError(ex.Message);
        }
    }

    private static TreeViewItem CreateTreeItem(MarkdownTreeNode node)
    {
        var item = new TreeViewItem { Header = node.Name, Tag = node };
        if (node.IsFile)
            return item;

        foreach (var child in node.Children)
            item.Items.Add(CreateTreeItem(child));

        return item;
    }

    private static Button ToolbarButton(string label) =>
        new() { Content = label, Margin = new Avalonia.Thickness(0, 0, 4, 0), Padding = new Avalonia.Thickness(10, 4) };
}
