using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Novolis.Avalonia.Markdown;
using TheArtOfDev.HtmlRenderer.Avalonia;

namespace ManuscriptStudio.Core;

/// <summary>HTML preview with theme, word-wrapping CSS, and Ctrl+scroll zoom.</summary>
internal sealed class HtmlPreviewPane : Border
{
    public static readonly StyledProperty<string?> DocumentBodyHtmlProperty =
        AvaloniaProperty.Register<HtmlPreviewPane, string?>(nameof(DocumentBodyHtml), string.Empty);

    public static readonly StyledProperty<double> ZoomScaleProperty =
        AvaloniaProperty.Register<HtmlPreviewPane, double>(nameof(ZoomScale), MarkdownZoom.Default);

    public static readonly StyledProperty<MarkdownPreviewTheme> PreviewThemeProperty =
        AvaloniaProperty.Register<HtmlPreviewPane, MarkdownPreviewTheme>(nameof(PreviewTheme), MarkdownPreviewTheme.StudioDark);

    private readonly HtmlPanel _html;
    private readonly ScrollViewer _scroll;

    public HtmlPreviewPane()
    {
        Background = new SolidColorBrush(Color.Parse("#1e1e1e"));
        BorderBrush = new SolidColorBrush(Color.Parse("#2d2d30"));
        BorderThickness = new Thickness(0);
        ClipToBounds = true;

        _html = new HtmlPanel { Margin = new Thickness(0) };

        _scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _html,
        };

        Child = _scroll;

        CtrlScrollZoom.Attach(this, () => ZoomScale, value => ZoomScale = value);
        CtrlScrollZoom.Attach(_scroll, () => ZoomScale, value => ZoomScale = value);
        CtrlScrollZoom.Attach(_html, () => ZoomScale, value => ZoomScale = value);

        RefreshHtml();
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

        if (change.Property == DocumentBodyHtmlProperty
            || change.Property == PreviewThemeProperty
            || change.Property == ZoomScaleProperty)
            RefreshHtml();
    }

    private void RefreshHtml()
    {
        var body = DocumentBodyHtml ?? string.Empty;
        _html.Text = MarkdownStudioHtml.WrapBody(body, PreviewTheme, ZoomScale);
        Background = PreviewTheme == MarkdownPreviewTheme.GitHubLight
            ? new SolidColorBrush(Color.Parse("#ffffff"))
            : new SolidColorBrush(Color.Parse("#1e1e1e"));
    }
}
