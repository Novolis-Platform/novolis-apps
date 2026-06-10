using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ManuscriptStudio.Core;

namespace ManuscriptStudio.Extensions.ConceptAssets;

/// <summary>Preview exported Concept Studio sheets linked from book writing workflow.</summary>
internal sealed class ConceptAssetExtension : IManuscriptExtension
{
    public const string ExtensionId = "concept-assets";

    private ManuscriptHostContext? _host;
    private TextBox? _pathBox;
    private TextBlock? _previewHint;
    private Image? _previewImage;
    private string? _conceptPath;

    public string Id => ExtensionId;
    public string DisplayName => "Concept Assets";
    public string DefaultRightRailViewId => RightRailViewDescriptor.Preview.Id;

    public Control CreateLeftRail(ManuscriptHostContext host)
    {
        _host = host;
        _pathBox = new TextBox { PlaceholderText = "Path to concept.json or export folder…", Margin = new Avalonia.Thickness(8) };
        var browse = ToolbarButton("Browse…");
        browse.Click += async (_, _) =>
        {
            var folder = await host.PickFolderAsync();
            if (folder is null)
                return;

            var conceptJson = Path.Combine(folder, "concept.json");
            _conceptPath = File.Exists(conceptJson) ? conceptJson : folder;
            _pathBox!.Text = _conceptPath;
            RefreshPreview(host);
        };

        var panel = new StackPanel { Spacing = 8, Margin = new Avalonia.Thickness(4) };
        panel.Children.Add(new TextBlock
        {
            Text = "Link a Concept Studio workspace or export folder. The preview shows the newest PNG in that folder.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.8,
            Margin = new Avalonia.Thickness(4),
        });
        panel.Children.Add(_pathBox);
        panel.Children.Add(browse);
        return panel;
    }

    public void ConfigureNavigationBar(StackPanel bar, ManuscriptHostContext host) => _host = host;

    public void ConfigureEditorBar(StackPanel bar, ManuscriptHostContext host)
    {
        _host = host;
        var reload = ToolbarButton("Reload preview");
        reload.Click += (_, _) => RefreshPreview(host);
        bar.Children.Add(reload);
    }

    public void ConfigurePreviewBar(StackPanel bar, ManuscriptHostContext host) => _host = host;

    public IReadOnlyList<RightRailViewDescriptor> GetRightRailViews() =>
        [RightRailViewDescriptor.Preview];

    public Control CreateRightRail(ManuscriptHostContext host, string viewId)
    {
        _host = host;
        _previewHint = new TextBlock
        {
            Text = "No concept export found. Export PNG or SVG from Concept Studio.",
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.75,
            Margin = new Avalonia.Thickness(16),
        };
        _previewImage = new Image
        {
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsVisible = false,
        };

        return new Grid
        {
            Children = { _previewHint, _previewImage },
        };
    }

    public void OnRightRailViewChanged(ManuscriptHostContext host, string viewId) => RefreshPreview(host);

    public void OnActivated(ManuscriptHostContext host)
    {
        _host = host;
        RefreshPreview(host);
    }

    public void OnDeactivated(ManuscriptHostContext host) => _host = null;

    private void RefreshPreview(ManuscriptHostContext host)
    {
        if (_previewHint is null || _previewImage is null)
            return;

        var path = _pathBox?.Text?.Trim();
        if (string.IsNullOrEmpty(path))
        {
            _previewHint.IsVisible = true;
            _previewImage.IsVisible = false;
            return;
        }

        var folder = File.Exists(path) ? Path.GetDirectoryName(path)! : path;
        if (!Directory.Exists(folder))
        {
            host.Feedback.FlashWarning("Concept path not found");
            return;
        }

        var png = Directory.EnumerateFiles(folder, "*.png", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
        if (png is null)
        {
            _previewHint.Text = "Folder linked — no PNG exports yet.";
            _previewHint.IsVisible = true;
            _previewImage.IsVisible = false;
            return;
        }

        try
        {
            _previewImage.Source = new Bitmap(png);
            _previewHint.IsVisible = false;
            _previewImage.IsVisible = true;
            host.Feedback.Flash($"Showing {Path.GetFileName(png)}");
        }
        catch (Exception ex)
        {
            host.Feedback.FlashError($"Preview failed: {ex.Message}");
        }
    }

    private static Button ToolbarButton(string text)
    {
        var button = new Button { Content = text, Margin = new Avalonia.Thickness(4, 0) };
        return button;
    }
}
