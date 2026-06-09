using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Avalonia.Markdown;
using TheArtOfDev.HtmlRenderer.Avalonia;

namespace ManuscriptStudio.Core;

/// <summary>HTML preview with dark/light themes, 10% side margins, and Ctrl+scroll zoom.</summary>
internal sealed class HtmlPreviewPane : Border
{
    public const double SideMarginFraction = 0.10;

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
    private string? _lastBodyHtml;

    public HtmlPreviewPane()
    {
        BorderThickness = new Thickness(0);
        ClipToBounds = true;

        _scale = new ScaleTransform(1, 1);
        _html = new HtmlPanel
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
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
        PointerWheelZoomRouting.Attach(_scroll, () => ZoomScale, value => ZoomScale = value);
        PointerWheelZoomRouting.Attach(_html, () => ZoomScale, value => ZoomScale = value);

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

        if (change.Property == DocumentBodyHtmlProperty)
        {
            _lastBodyHtml = DocumentBodyHtml;
            RefreshHtml();
        }
        else if (change.Property == PreviewThemeProperty)
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
        var body = _lastBodyHtml ?? DocumentBodyHtml ?? string.Empty;
        _html.Text = MarkdownStudioHtml.WrapBody(body, PreviewTheme, zoomScale: 1.0);
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
        if (viewportWidth <= 0)
            return;

        var sideInset = viewportWidth * SideMarginFraction;
        var contentWidth = Math.Max(1, viewportWidth - (2 * sideInset));
        var layoutWidth = contentWidth / zoom;

        _html.Margin = new Thickness(sideInset, 0, sideInset, 0);
        _html.Width = layoutWidth;
        _html.MaxWidth = layoutWidth;
    }
}
