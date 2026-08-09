using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Novolis.Avalonia.Markdown;

namespace BooksWriterStudio.Ui;

/// <summary>
/// Scratch reading surface: open any .txt/.md (or paste clipboard) and render it for reading / speak.
/// Independent of the manuscript workspace.
/// </summary>
internal sealed class ReadAnythingPane : Border
{
    readonly MarkdownPreviewPane _preview = new()
    {
        PreviewTheme = MarkdownPreviewTheme.StudioDark,
        ZoomScale = 1.08,
        SideMarginFraction = 0.10,
        IsVisible = false,
    };

    readonly TextBox _pasteBox = new()
    {
        AcceptsReturn = true,
        TextWrapping = TextWrapping.Wrap,
        PlaceholderText = "Paste or type any text / markdown here…",
        MinHeight = 180,
        MaxHeight = 320,
    };

    readonly TextBlock _emptyHint = new()
    {
        Text = "Open a .txt or .md file, or paste anything to read it here.",
        TextWrapping = TextWrapping.Wrap,
        Opacity = 0.8,
        FontSize = 15,
        HorizontalAlignment = HorizontalAlignment.Center,
        TextAlignment = TextAlignment.Center,
        Margin = new Thickness(0, 0, 0, 16),
    };

    readonly TextBlock _sourceLabel = new()
    {
        VerticalAlignment = VerticalAlignment.Center,
        Opacity = 0.85,
        TextTrimming = TextTrimming.CharacterEllipsis,
    };

    readonly StackPanel _emptyHost;
    readonly Border _chromeBar;
    string _documentText = string.Empty;

    public ReadAnythingPane()
    {
        Background = new SolidColorBrush(Color.Parse("#1e1e1e"));
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;

        var openBtn = ToolButton("Open file…", async () => await OpenFileAsync());
        var pasteBtn = ToolButton("Paste", async () => await PasteClipboardAsync());
        var applyBtn = ToolButton("Read pasted", () => ApplyPasteBox());
        var clearBtn = ToolButton("Clear", Clear);
        var speakBtn = ToolButton("Speak", () => SpeakRequested?.Invoke(this, EventArgs.Empty));
        var closeBtn = ToolButton("Back to writing", () => CloseRequested?.Invoke(this, EventArgs.Empty));

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children = { openBtn, pasteBtn, applyBtn, clearBtn, speakBtn, closeBtn },
        };

        var barInner = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(actions, Dock.Left);
        barInner.Children.Add(actions);
        _sourceLabel.Margin = new Thickness(16, 0, 0, 0);
        barInner.Children.Add(_sourceLabel);

        _chromeBar = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#252526")),
            BorderBrush = new SolidColorBrush(Color.Parse("#3F3F46")),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(10, 8),
            Child = barInner,
        };

        _emptyHost = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            MaxWidth = 560,
            Margin = new Thickness(24),
            Spacing = 8,
            Children = { _emptyHint, _pasteBox },
        };

        var content = new Grid();
        content.Children.Add(_emptyHost);
        content.Children.Add(_preview);

        var root = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(_chromeBar, Dock.Top);
        root.Children.Add(_chromeBar);
        root.Children.Add(content);
        Child = root;

        KeyDown += OnKeyDown;
        Focusable = true;
        ShowEmpty();
    }

    public event EventHandler? CloseRequested;
    public event EventHandler? SpeakRequested;

    public string DocumentText => _documentText;

    public bool HasDocument => !string.IsNullOrWhiteSpace(_documentText);

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
                },
                new FilePickerFileType("All files")
                {
                    Patterns = ["*.*"],
                },
            ],
        });

        if (files.Count == 0)
            return;

        var path = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        var text = await File.ReadAllTextAsync(path);
        LoadText(text, path);
    }

    public async Task PasteClipboardAsync()
    {
        var top = TopLevel.GetTopLevel(this);
        var clip = top?.Clipboard;
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

    void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.V && !_pasteBox.IsFocused)
        {
            _ = PasteClipboardAsync();
            e.Handled = true;
        }
    }

    static Button ToolButton(string label, Action click)
    {
        var btn = new Button { Content = label };
        btn.Click += (_, _) => click();
        return btn;
    }

    static Button ToolButton(string label, Func<Task> click)
    {
        var btn = new Button { Content = label };
        btn.Click += async (_, _) => await click();
        return btn;
    }
}
