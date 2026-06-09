using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Avalonia.Markdown;
using TheArtOfDev.HtmlRenderer.Avalonia;

namespace ManuscriptStudio.Core;

/// <summary>HTML preview with dark/light themes, panel-width wrapping, and Ctrl+scroll zoom.</summary>
internal sealed class HtmlPreviewPane : Border
{
    public static readonly StyledProperty<string?> DocumentBodyHtmlProperty =
        AvaloniaProperty.Register<HtmlPreviewPane, string?>(nameof(DocumentBodyHtml), string.Empty);

    public static readonly StyledProperty<double> ZoomScaleProperty =
        AvaloniaProperty.Register<HtmlPreviewPane, double>(nameof(ZoomScale), MarkdownZoom.Default);

    public static readonly StyledProperty<MarkdownPreviewTheme> PreviewThemeProperty =
        AvaloniaProperty.Register<HtmlPreviewPane, MarkdownPreviewTheme>(nameof(PreviewTheme), MarkdownPreviewTheme.StudioDark);

    public event EventHandler<double>? ZoomScaleChanged;

    private readonly HtmlPanel _html;
    private readonly ScrollViewer _scroll;
    private readonly ScaleTransform _scale;
    private string? _lastHtml;

    public HtmlPreviewPane()
    {
        BorderThickness = new Thickness(0);
        ClipToBounds = true;

        _scale = new ScaleTransform(1, 1);
        _html = new HtmlPanel
        {
            Margin = new Thickness(0),
            RenderTransform = _scale,
            RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative),
        };

        _scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _html,
        };

        Child = _scroll;

        _scroll.SizeChanged += (_, _) => ApplyLayout();
        CtrlScrollZoom.Attach(this, () => ZoomScale, value => ZoomScale = value);
        CtrlScrollZoom.Attach(_scroll, () => ZoomScale, value => ZoomScale = value);
        CtrlScrollZoom.Attach(_html, () => ZoomScale, value => ZoomScale = value);

        ApplyThemeChrome();
    }

    public string? DocumentBodyHtml
    {
        get => GetValue(DocumentBodyHtmlProperty);
        set => SetValue(DocumentBodyHtmlProperty, value);
    }

    public double ZoomScale
    {
        get => GetValue(ZoomScaleProperty);
        set => SetValue(ZoomScaleProperty, value);
    }

    public MarkdownPreviewTheme PreviewTheme
    {
        get => GetValue(PreviewThemeProperty);
        set => SetValue(PreviewThemeProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == DocumentBodyHtmlProperty || change.Property == PreviewThemeProperty)
        {
            ApplyThemeChrome();
            RefreshHtml();
        }
        else if (change.Property == ZoomScaleProperty)
        {
            ApplyLayout();
            ZoomScaleChanged?.Invoke(this, ZoomScale);
        }
    }

    private void ApplyThemeChrome()
    {
        if (PreviewTheme == MarkdownPreviewTheme.GitHubLight)
        {
            Background = new SolidColorBrush(Color.Parse("#ffffff"));
            _html.Background = new SolidColorBrush(Color.Parse("#ffffff"));
        }
        else
        {
            Background = new SolidColorBrush(Color.Parse("#1e1e1e"));
            _html.Background = new SolidColorBrush(Color.Parse("#1e1e1e"));
        }
    }

    private void RefreshHtml()
    {
        var body = DocumentBodyHtml ?? string.Empty;
        _lastHtml = MarkdownStudioHtml.WrapBody(body, PreviewTheme, 1.0);
        _html.Text = _lastHtml;
        ApplyThemeChrome();
        ApplyLayout();
    }

    private void ApplyLayout()
    {
        var zoom = Math.Clamp(ZoomScale, MarkdownZoom.Minimum, MarkdownZoom.Maximum);
        if (Math.Abs(zoom - ZoomScale) > 0.0001)
            SetCurrentValue(ZoomScaleProperty, zoom);

        _scale.ScaleX = zoom;
        _scale.ScaleY = zoom;

        var viewportWidth = _scroll.Viewport.Width;
        if (viewportWidth > 0)
        {
            var layoutWidth = viewportWidth / zoom;
            _html.MaxWidth = layoutWidth;
            _html.Width = layoutWidth;
        }

        if (_lastHtml is not null && !string.IsNullOrEmpty(_lastHtml))
            _html.Text = _lastHtml;
    }
}
