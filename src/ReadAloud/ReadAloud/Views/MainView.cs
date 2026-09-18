using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ReadAloud.Services;
using ReadAloud.Ui;

namespace ReadAloud.Views;

/// <summary>Paste or open text, listen with Edge TTS, or save an MP3.</summary>
public sealed class MainView : DockPanel
{
    readonly SpeechService _speech;
    readonly IScreenWakeLock _wakeLock;
    readonly TextBox _textBox;
    readonly Button _listenButton;
    readonly Button _saveButton;
    readonly TextBlock _status;
    IDisposable? _wake;

    public MainView(SpeechService speech, IScreenWakeLock wakeLock)
    {
        _speech = speech ?? throw new ArgumentNullException(nameof(speech));
        _wakeLock = wakeLock ?? throw new ArgumentNullException(nameof(wakeLock));

        LastChildFill = true;
        Background = ReadAloudPalette.WindowBrush;

        _textBox = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            PlaceholderText = "Paste or type anything to hear…",
            FontFamily = ReadAloudPalette.BodyFont,
            FontSize = 16,
            Foreground = ReadAloudPalette.BodyBrush,
            Background = ReadAloudPalette.PanelRaisedBrush,
            CaretBrush = ReadAloudPalette.AccentBrush,
        };

        var openBtn = ReadAloudTheme.Button("Open file…", ReadAloudButtonKind.Secondary);
        openBtn.Click += async (_, _) => await OpenFileAsync();
        var pasteBtn = ReadAloudTheme.Button("Paste", ReadAloudButtonKind.Secondary);
        pasteBtn.Click += async (_, _) => await PasteClipboardAsync();
        _listenButton = ReadAloudTheme.Button("Listen", ReadAloudButtonKind.Primary);
        _listenButton.Click += async (_, _) => await OnListenClickAsync();
        _saveButton = ReadAloudTheme.Button("Save MP3", ReadAloudButtonKind.Secondary);
        _saveButton.Click += async (_, _) => await SaveMp3Async();
        var clearBtn = ReadAloudTheme.Button("Clear", ReadAloudButtonKind.Quiet);
        clearBtn.Click += (_, _) => Clear();

        var actions = new WrapPanel
        {
            Margin = new Thickness(0, 8, 0, 0),
        };
        foreach (var btn in new[] { openBtn, pasteBtn, _listenButton, _saveButton, clearBtn })
        {
            btn.Margin = new Thickness(0, 0, 8, 8);
            actions.Children.Add(btn);
        }

        _status = ReadAloudTheme.Muted("Needs network for Edge TTS. Voice: Ava (narrator).");

        var chrome = new StackPanel
        {
            Spacing = 4,
            Margin = new Thickness(16, 12, 16, 8),
            Children =
            {
                ReadAloudTheme.BrandTitle("Read Aloud", 26),
                ReadAloudTheme.Muted("Scratch reader — listen now, or write an MP3."),
                actions,
                _status,
            },
        };

        SetDock(chrome, Dock.Top);
        Children.Add(chrome);
        Children.Add(new Border
        {
            Margin = new Thickness(16, 0, 16, 16),
            Padding = new Thickness(12),
            Background = ReadAloudPalette.PanelBrush,
            CornerRadius = new CornerRadius(6),
            BorderBrush = new SolidColorBrush(Color.FromArgb(40, 47, 223, 255)),
            BorderThickness = new Thickness(1),
            Child = _textBox,
        });

        _speech.Changed += (_, _) => Dispatcher.UIThread.Post(RefreshListen);
        _speech.PlaybackStarted += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            if (_speech.IsSpeaking)
                SetStatus("Playing…");
        });
        DetachedFromVisualTree += (_, _) =>
        {
            _speech.Stop();
            ReleaseWake();
        };
    }

    string DocumentText => _textBox.Text ?? string.Empty;

    bool HasDocument => !string.IsNullOrWhiteSpace(DocumentText);

    void Clear()
    {
        _speech.Stop();
        _textBox.Text = string.Empty;
        SetStatus("Needs network for Edge TTS. Voice: Ava (narrator).");
        RefreshListen();
    }

    async Task OpenFileAsync()
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
        _textBox.Text = await reader.ReadToEndAsync();
        var label = files[0].Name;
        if (string.IsNullOrWhiteSpace(label))
            label = files[0].TryGetLocalPath() ?? "Opened file";
        SetStatus($"Opened {label}");
        RefreshListen();
    }

    async Task PasteClipboardAsync()
    {
        var clip = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clip is null)
            return;

        var text = await clip.TryGetTextAsync();
        if (string.IsNullOrWhiteSpace(text))
        {
            SetStatus("Clipboard is empty.");
            return;
        }

        _textBox.Text = text;
        SetStatus("Pasted clipboard.");
        RefreshListen();
    }

    async Task OnListenClickAsync()
    {
        if (_speech.IsSpeaking)
        {
            _speech.Stop();
            ReleaseWake();
            SetStatus("Stopped.");
            RefreshListen();
            return;
        }

        var text = DocumentText;
        if (string.IsNullOrWhiteSpace(text))
        {
            SetStatus("Paste or type something first.");
            return;
        }

        _wake = _wakeLock.Acquire("read-aloud-listen");
        SetStatus("Synthesizing…");
        RefreshListen();
        try
        {
            await _speech.SpeakAsync(text);
            if (!_speech.IsSpeaking)
                SetStatus(_speech.HasCachedAudio(text) ? "Done. Audio is cached." : "Done.");
        }
        catch (OperationCanceledException)
        {
            SetStatus("Stopped.");
        }
        catch (Exception ex)
        {
            SetStatus($"Listen failed: {ex.Message}");
        }
        finally
        {
            ReleaseWake();
            RefreshListen();
        }
    }

    async Task SaveMp3Async()
    {
        var text = DocumentText;
        if (string.IsNullOrWhiteSpace(text))
        {
            SetStatus("Paste or type something first.");
            return;
        }

        var top = TopLevel.GetTopLevel(this);
        if (top?.StorageProvider is null)
            return;

        if (_speech.IsSpeaking)
            _speech.Stop();

        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save MP3",
            SuggestedFileName = "read-aloud.mp3",
            DefaultExtension = "mp3",
            FileTypeChoices =
            [
                new FilePickerFileType("MP3 audio")
                {
                    Patterns = ["*.mp3"],
                    MimeTypes = ["audio/mpeg"],
                },
            ],
        });

        if (file is null)
            return;

        _saveButton.IsEnabled = false;
        SetStatus("Writing MP3…");
        try
        {
            var mp3 = await _speech.SynthesizeDocumentMp3Async(text);
            if (mp3.Length == 0)
            {
                SetStatus("Nothing to save — Edge TTS returned empty audio.");
                return;
            }

            await using var stream = await file.OpenWriteAsync();
            await stream.WriteAsync(mp3);
            SetStatus($"Saved {file.Name} ({mp3.Length:N0} bytes).");
        }
        catch (OperationCanceledException)
        {
            SetStatus("Save cancelled.");
        }
        catch (Exception ex)
        {
            SetStatus($"Save failed: {ex.Message}");
        }
        finally
        {
            _saveButton.IsEnabled = true;
            RefreshListen();
        }
    }

    void RefreshListen()
    {
        var speaking = _speech.IsSpeaking;
        _listenButton.Content = speaking ? "Stop" : (_speech.HasCachedAudio(DocumentText) ? "Listen ✓" : "Listen");
        ReadAloudTheme.StyleButton(_listenButton, ReadAloudButtonKind.Primary);
        _saveButton.IsEnabled = !speaking;
    }

    void SetStatus(string text) => _status.Text = text;

    void ReleaseWake()
    {
        _wake?.Dispose();
        _wake = null;
    }
}
