using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using BooksMobile.Services;

namespace BooksMobile.Ui;

/// <summary>Paste a ChatGPT Review selection that follows the house template, then import.</summary>
internal sealed class PasteSelectionView : DockPanel
{
    readonly TextBox _pasteBox = new()
    {
        AcceptsReturn = true,
        TextWrapping = TextWrapping.Wrap,
        PlaceholderText = "Paste full selection markdown (YAML + masthead + ## Article)…",
        MinHeight = 220,
        FontFamily = BooksPalette.BodyFont,
        FontSize = 14,
        Foreground = BooksPalette.BodyBrush,
        Background = BooksPalette.PanelRaisedBrush,
    };

    readonly TextBlock _status = BooksTheme.Muted(string.Empty, 13);

    public PasteSelectionView()
    {
        LastChildFill = true;
        Background = BooksPalette.WindowBrush;

        var pasteBtn = BooksTheme.Button("Paste clipboard", BooksButtonKind.Secondary);
        pasteBtn.Click += async (_, _) => await PasteClipboardAsync();
        var templateBtn = BooksTheme.Button("Copy template", BooksButtonKind.Quiet);
        templateBtn.Click += async (_, _) => await CopyTemplateAsync();
        var importBtn = BooksTheme.Button("Import + audio + push", BooksButtonKind.Primary);
        importBtn.Click += (_, _) => ImportRequested?.Invoke(this, EventArgs.Empty);

        foreach (var btn in new[] { pasteBtn, templateBtn, importBtn })
        {
            btn.HorizontalAlignment = HorizontalAlignment.Left;
            btn.MinWidth = 0;
            btn.Margin = new Thickness(0, 0, 8, 8);
        }

        var actions = new WrapPanel { Children = { pasteBtn, templateBtn, importBtn } };
        var chrome = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(12, 8),
            Children =
            {
                BooksTheme.BrandTitle("Paste selection", 22),
                BooksTheme.Muted(
                    "Galactic Confederation Review only. Follow the template, then import writes docs/articles, audio, indexes, and pushes main."),
                actions,
                _status,
            },
        };

        SetDock(chrome, Dock.Top);
        Children.Add(chrome);
        Children.Add(new ScrollViewer
        {
            Margin = new Thickness(12, 0, 12, 12),
            Content = _pasteBox,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        });
    }

    public event EventHandler? ImportRequested;

    public string PastedText => _pasteBox.Text ?? string.Empty;

    public void SetStatus(string text) => _status.Text = text ?? string.Empty;

    public void Clear()
    {
        _pasteBox.Text = string.Empty;
        _status.Text = string.Empty;
    }

    async Task PasteClipboardAsync()
    {
        var clip = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clip is null)
            return;
        var text = await clip.TryGetTextAsync();
        if (string.IsNullOrWhiteSpace(text))
        {
            SetStatus("Clipboard has no text.");
            return;
        }

        _pasteBox.Text = text;
        SetStatus("Pasted — review, then Import + audio + push.");
    }

    async Task CopyTemplateAsync()
    {
        var clip = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clip is null)
            return;
        await clip.SetTextAsync(ReviewSelectionImporter.TemplateBlurb);
        SetStatus("Template copied — paste into ChatGPT, then paste the result here.");
    }
}
