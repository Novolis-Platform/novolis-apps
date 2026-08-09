using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Novolis.Avalonia.Markdown;

namespace BooksMobile.Ui;

/// <summary>
/// Scratch reading surface for arbitrary pasted text or opened .txt/.md files.
/// Independent of the manuscript library.
/// </summary>
internal sealed class ReadAnythingView : DockPanel
{
    readonly MarkdownPreviewPane _preview = new()
    {
        PreviewTheme = MarkdownPreviewTheme.StudioDark,
        ZoomScale = 1.08,
        SideMarginFraction = 0.06,
        IsVisible = false,
    };

    readonly TextBox _pasteBox = new()
    {
        AcceptsReturn = true,
        TextWrapping = TextWrapping.Wrap,
        PlaceholderText = "Paste or type any text / markdown…",
        MinHeight = 160,
        FontFamily = BooksPalette.BodyFont,
        FontSize = 15,
        Foreground = BooksPalette.BodyBrush,
        Background = BooksPalette.PanelRaisedBrush,
    };

    readonly TextBlock _sourceLabel = BooksTheme.Muted(string.Empty, 12);
    readonly StackPanel _emptyHost;
    readonly Button _listenButton;
    string _documentText = string.Empty;

    public ReadAnythingView()
    {
        LastChildFill = true;
        Background = BooksPalette.WindowBrush;

        var openBtn = BooksTheme.Button("Open file…", BooksButtonKind.Secondary);
        openBtn.Click += async (_, _) => await OpenFileAsync();
        var pasteBtn = BooksTheme.Button("Paste", BooksButtonKind.Secondary);
        pasteBtn.Click += async (_, _) => await PasteClipboardAsync();
        var readBtn = BooksTheme.Button("Read pasted", BooksButtonKind.Primary);
        readBtn.Click += (_, _) => ApplyPasteBox();
        var clearBtn = BooksTheme.Button("Clear", BooksButtonKind.Quiet);
        clearBtn.Click += (_, _) => Clear();
        _listenButton = BooksTheme.Button("Listen", BooksButtonKind.Primary);
        _listenButton.Click += (_, _) => ListenRequested?.Invoke(this, EventArgs.Empty);

        var actions = new WrapPanel
        {
            Margin = new Thickness(0, 0, 0, 8),
        };
        foreach (var btn in new[] { openBtn, pasteBtn, readBtn, clearBtn, _listenButton })
        {
            btn.HorizontalAlignment = HorizontalAlignment.Left;
            btn.MinWidth = 0;
            btn.Margin = new Thickness(0, 0, 8, 8);
            actions.Children.Add(btn);
        }

        var chrome = new StackPanel
        {
            Spacing = 4,
            Margin = new Thickness(12, 8),
            Children =
            {
                BooksTheme.BrandTitle("Read anything", 22),
                BooksTheme.Muted("Scratch reader — not part of your library / protocol tree."),
                actions,
                _sourceLabel,
            },
        };

        _emptyHost = new StackPanel
        {
            Spacing = 10,
            Margin = new Thickness(16, 0, 16, 16),
            Children = { _pasteBox },
        };

        var content = new Grid();
        content.Children.Add(_emptyHost);
        content.Children.Add(_preview);

        SetDock(chrome, Dock.Top);
        Children.Add(chrome);
        Children.Add(content);
        ShowEmpty();
    }

    public event EventHandler? ListenRequested;

    public string DocumentText => _documentText;

    public bool HasDocument => !string.IsNullOrWhiteSpace(_documentText);

    public void SetListening(bool speaking, bool hasCache)
    {
        _listenButton.Content = speaking ? "Stop" : (hasCache ? "Listen ✓" : "Listen");
        BooksTheme.StyleButton(_listenButton, BooksButtonKind.Primary);
    }

    public void LoadText(string text, string? sourceLabel)
    {
        _documentText = text ?? string.Empty;
        _pasteBox.Text = _documentText;
        _preview.Markdown = _documentText;
        _sourceLabel.Text = string.IsNullOrWhiteSpace(sourceLabel)
            ? (HasDocument ? "Pasted text" : string.Empty)
            : sourceLabel;
        ShowDocument();
    }

    public void Clear()
    {
        _documentText = string.Empty;
        _pasteBox.Text = string.Empty;
        _preview.Markdown = string.Empty;
        _sourceLabel.Text = string.Empty;
        ShowEmpty();
    }

    public async Task OpenFileAsync()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top?.StorageProvider is null)
            return;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open text to read",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Text / Markdown")
                {
                    Patterns = ["*.txt", "*.md", "*.markdown", "*.text"],
                    MimeTypes = ["text/plain", "text/markdown"],
                },
                new FilePickerFileType("All files")
                {
                    Patterns = ["*.*"],
                },
            ],
        });

        if (files.Count == 0)
            return;

        await using var stream = await files[0].OpenReadAsync();
        using var reader = new StreamReader(stream);
        var text = await reader.ReadToEndAsync();
        var label = files[0].Name;
        if (string.IsNullOrWhiteSpace(label))
            label = files[0].TryGetLocalPath() ?? "Opened file";
        LoadText(text, label);
    }

    public async Task PasteClipboardAsync()
    {
        var clip = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clip is null)
            return;

        var text = await clip.TryGetTextAsync();
        if (string.IsNullOrWhiteSpace(text))
            return;

        LoadText(text, "Clipboard");
    }

    void ApplyPasteBox()
    {
        var text = _pasteBox.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return;
        LoadText(text, "Pasted text");
    }

    void ShowEmpty()
    {
        _emptyHost.IsVisible = true;
        _preview.IsVisible = false;
    }

    void ShowDocument()
    {
        _emptyHost.IsVisible = !HasDocument;
        _preview.IsVisible = HasDocument;
        if (HasDocument)
            _preview.Refresh();
    }
}
