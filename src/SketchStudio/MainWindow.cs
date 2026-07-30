using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Novolis.Avalonia.Controls;
using Optris.Icons.Avalonia;

namespace SketchStudio;

internal sealed class MainWindow : Window
{
    static readonly string[] Palette =
    [
        "#1e1e1e", "#e63946", "#f4a261", "#2a9d8f", "#457b9d",
        "#6d597a", "#ffffff", "#adb5bd"
    ];

    static readonly FilePickerFileType SketchJsonType = new("Sketch JSON")
    {
        Patterns = ["*.sketchjson"]
    };

    readonly SketchControl _sketch;
    readonly TextBlock _status;
    readonly Slider _gridSlider;
    readonly Slider _widthSlider;
    readonly List<ToggleButton> _toolButtons = [];
    readonly Dictionary<SketchTool, ToggleButton> _toolByKind = new();
    readonly List<ToggleButton> _styleButtons = [];
    readonly CheckBox _snapBox;
    readonly CheckBox _meetupBox;
    readonly CheckBox _gridBox;
    readonly CheckBox _fillBox;
    readonly Border _colorPreview;

    string? _documentPath;
    bool _isDirty;
    bool _suppressDirty;
    bool _closeConfirmed;

    public MainWindow()
    {
        Width = 1180;
        Height = 760;
        MinWidth = 720;
        MinHeight = 480;

        _sketch = new SketchControl
        {
            Tool = SketchTool.Pen,
            GridSize = 20,
            GridVisible = true,
            SnapEnabled = true,
            MeetupEnabled = true,
            StrokeColor = "#1e1e1e",
            StrokeWidth = 2
        };
        _sketch.DocumentChanged += OnSketchDocumentChanged;
        _sketch.SelectionChanged += RefreshStatus;

        _status = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.8,
            Margin = new Thickness(8, 0, 0, 0),
            FontSize = 12
        };

        _colorPreview = new Border
        {
            Width = 22,
            Height = 22,
            CornerRadius = new CornerRadius(4),
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            Background = BrushFromHex(_sketch.StrokeColor),
            VerticalAlignment = VerticalAlignment.Center
        };

        _snapBox = Toggle("Snap to grid", true, v => _sketch.SnapEnabled = v);
        _meetupBox = Toggle("Meetup", true, v => _sketch.MeetupEnabled = v);
        _gridBox = Toggle("Grid", true, v => _sketch.GridVisible = v);
        _fillBox = Toggle("Fill", false, v =>
        {
            _sketch.FillEnabled = v;
            RefreshStatus();
        });

        _gridSlider = new Slider
        {
            Minimum = 5,
            Maximum = 80,
            Value = 20,
            Width = 100,
            VerticalAlignment = VerticalAlignment.Center
        };
        _gridSlider.PropertyChanged += (_, e) =>
        {
            if (e.Property == RangeBase.ValueProperty)
                _sketch.GridSize = _gridSlider.Value;
        };

        _widthSlider = new Slider
        {
            Minimum = 0.25,
            Maximum = 16,
            Value = 2,
            Width = 110,
            VerticalAlignment = VerticalAlignment.Center
        };
        _widthSlider.PropertyChanged += (_, e) =>
        {
            if (e.Property == RangeBase.ValueProperty)
            {
                _sketch.StrokeWidth = _widthSlider.Value;
                RefreshStatus();
            }
        };

        var file = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Children =
            {
                IconButton("fa-solid fa-file", "New (Ctrl+N)", () => _ = NewAsync()),
                IconButton("fa-solid fa-folder-open", "Open (Ctrl+O)", () => _ = OpenAsync()),
                IconButton("fa-solid fa-floppy-disk", "Save (Ctrl+S)", () => _ = SaveAsync()),
                IconButton("fa-solid fa-file-export", "Save As (Ctrl+Shift+S)", () => _ = SaveAsAsync()),
            }
        };

        var tools = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Children =
            {
                ToolBtn("fa-solid fa-pen", "Pen (P)", SketchTool.Pen, selected: true),
                ToolBtn("fa-solid fa-slash", "Line (L)", SketchTool.Line),
                ToolBtn("fa-solid fa-bezier-curve", "Spline (S)", SketchTool.Spline),
                ToolBtn("fa-regular fa-square", "Box (R)", SketchTool.Rect),
                ToolBtn("fa-regular fa-circle", "Circle (C)", SketchTool.Ellipse),
                ToolBtn("fa-solid fa-eraser", "Eraser (E)", SketchTool.Eraser),
                ToolBtn("fa-solid fa-mouse-pointer", "Select (V)", SketchTool.Select),
                Sep(),
                IconButton("fa-solid fa-check", "Complete line/spline (Enter)", () =>
                {
                    _sketch.CompleteDrawing(closeShape: false);
                    SetStatus(_sketch.HasInProgressDrawing ? "Still drawing…" : "Completed.");
                }),
                IconButton("fa-solid fa-draw-polygon", "Close shape (Ctrl+Enter / click start)", () =>
                {
                    _sketch.CompleteDrawing(closeShape: true);
                    SetStatus(_sketch.HasInProgressDrawing ? "Need ≥3 points to close." : "Closed.");
                }),
            }
        };

        var colors = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        colors.Children.Add(_colorPreview);
        foreach (var hex in Palette)
        {
            var swatch = hex;
            var btn = new Button
            {
                Width = 22,
                Height = 22,
                Padding = new Thickness(0),
                Background = BrushFromHex(swatch),
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3)
            };
            ToolTip.SetTip(btn, swatch);
            btn.Click += (_, _) => SetStrokeColor(swatch);
            colors.Children.Add(btn);
        }

        var styles = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        styles.Children.Add(StyleBtn("────", "Solid", SketchStrokeStyle.Solid, selected: true));
        styles.Children.Add(StyleBtn("- - -", "Dashed", SketchStrokeStyle.Dashed));
        styles.Children.Add(StyleBtn("····", "Dotted", SketchStrokeStyle.Dotted));
        styles.Children.Add(StyleBtn("-·-", "Dash-dot", SketchStrokeStyle.DashDot));
        styles.Children.Add(StyleBtn("·····", "Stipple", SketchStrokeStyle.Stipple));

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                IconButton("fa-solid fa-table-cells", "Gridify", () =>
                {
                    _sketch.GridifySelection();
                    SetStatus("Gridified.");
                }),
                IconButton("fa-solid fa-rotate-left", "Undo (Ctrl+Z)", () => _sketch.Undo()),
                IconButton("fa-solid fa-rotate-right", "Redo (Ctrl+Y)", () => _sketch.Redo()),
                IconButton("fa-solid fa-trash", "Clear", () =>
                {
                    _sketch.Clear();
                    SetStatus("Cleared.");
                }),
                IconButton("fa-solid fa-image", "Copy PNG", () => _ = CopyPngAsync()),
                IconButton("fa-solid fa-code", "Copy SVG", () => _ = CopySvgAsync()),
            }
        };

        var row1 = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Margin = new Thickness(10, 8, 10, 4),
            Children =
            {
                file,
                Sep(),
                tools,
                Sep(),
                _snapBox,
                _meetupBox,
                _gridBox,
                _fillBox,
                Label("Grid"),
                _gridSlider,
                Label("Width"),
                _widthSlider,
            }
        };

        var row2 = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Margin = new Thickness(10, 0, 10, 4),
            Children =
            {
                Label("Color"),
                colors,
                Sep(),
                Label("Stroke"),
                styles,
                Sep(),
                actions,
                _status
            }
        };

        var hint = new TextBlock
        {
            Text = "Ctrl+N/O/S · Line/Spline: click points · Done (Enter) or Close (Ctrl+Enter / click first point) · Fill applies to closed shapes · Stroke swatches: solid/dash/dot/stipple · Shift+circle for perfect · Del deletes selection",
            Opacity = 0.6,
            FontSize = 11,
            Margin = new Thickness(12, 0, 12, 6),
            TextWrapping = TextWrapping.Wrap
        };

        var top = new StackPanel { Children = { row1, row2, hint } };
        var root = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        root.Children.Add(_sketch);
        Content = root;

        KeyDown += OnKeyDown;
        Closing += OnClosing;
        RefreshTitle();
        RefreshStatus();
    }

    void OnSketchDocumentChanged()
    {
        if (!_suppressDirty)
            _isDirty = true;
        RefreshTitle();
        RefreshStatus();
    }

    void RefreshTitle()
    {
        var name = string.IsNullOrWhiteSpace(_documentPath)
            ? "Untitled"
            : Path.GetFileName(_documentPath);
        Title = _isDirty
            ? $"Sketch Studio — {name}*"
            : $"Sketch Studio — {name}";
    }

    void SetStrokeColor(string hex)
    {
        _sketch.StrokeColor = hex;
        if (_sketch.FillEnabled)
            _sketch.FillColor = hex;
        _colorPreview.Background = BrushFromHex(hex);
        RefreshStatus();
    }

    void SelectTool(SketchTool tool)
    {
        _sketch.Tool = tool;
        foreach (var (kind, btn) in _toolByKind)
            btn.IsChecked = kind == tool;
        RefreshStatus();
    }

    ToggleButton ToolBtn(string icon, string tip, SketchTool tool, bool selected = false)
    {
        var btn = new ToggleButton
        {
            Width = 36,
            Height = 32,
            IsChecked = selected,
            Content = new Icon { Value = icon, FontSize = 14 }
        };
        ToolTip.SetTip(btn, tip);
        btn.IsCheckedChanged += (_, _) =>
        {
            if (btn.IsChecked != true)
                return;
            foreach (var other in _toolButtons)
            {
                if (!ReferenceEquals(other, btn))
                    other.IsChecked = false;
            }

            _sketch.Tool = tool;
            RefreshStatus();
        };
        _toolButtons.Add(btn);
        _toolByKind[tool] = btn;
        return btn;
    }

    ToggleButton StyleBtn(string glyph, string tip, SketchStrokeStyle style, bool selected = false)
    {
        var btn = new ToggleButton
        {
            MinWidth = 36,
            Height = 28,
            Padding = new Thickness(6, 0),
            IsChecked = selected,
            Content = new TextBlock
            {
                Text = glyph,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            }
        };
        ToolTip.SetTip(btn, tip);
        btn.IsCheckedChanged += (_, _) =>
        {
            if (btn.IsChecked != true)
                return;
            foreach (var other in _styleButtons)
            {
                if (!ReferenceEquals(other, btn))
                    other.IsChecked = false;
            }

            _sketch.StrokeStyle = style;
            RefreshStatus();
        };
        _styleButtons.Add(btn);
        return btn;
    }

    static Button IconButton(string icon, string tip, Action action)
    {
        var b = new Button
        {
            Width = 36,
            Height = 32,
            Content = new Icon { Value = icon, FontSize = 14 },
            Padding = new Thickness(0)
        };
        ToolTip.SetTip(b, tip);
        b.Click += (_, _) => action();
        return b;
    }

    static CheckBox Toggle(string label, bool on, Action<bool> set)
    {
        var box = new CheckBox
        {
            Content = label,
            IsChecked = on,
            VerticalAlignment = VerticalAlignment.Center
        };
        box.IsCheckedChanged += (_, _) => set(box.IsChecked == true);
        return box;
    }

    static TextBlock Label(string text) => new()
    {
        Text = text,
        VerticalAlignment = VerticalAlignment.Center,
        Opacity = 0.75,
        FontSize = 12
    };

    static Control Sep() => new Border
    {
        Width = 1,
        Background = Brushes.Gray,
        Opacity = 0.35,
        Margin = new Thickness(4, 2)
    };

    static IBrush BrushFromHex(string hex)
    {
        try { return new SolidColorBrush(Color.Parse(hex)); }
        catch { return Brushes.Black; }
    }

    async Task NewAsync()
    {
        if (!await ConfirmDiscardIfDirtyAsync())
            return;

        _suppressDirty = true;
        try
        {
            _sketch.Document = new SketchDocument
            {
                Grid =
                {
                    Size = _sketch.GridSize,
                    Visible = _sketch.GridVisible,
                    SnapEnabled = _sketch.SnapEnabled
                }
            };
            _documentPath = null;
            _isDirty = false;
        }
        finally
        {
            _suppressDirty = false;
        }

        SyncUiFromDocument();
        RefreshTitle();
        SetStatus("New sketch.");
    }

    async Task OpenAsync()
    {
        if (!await ConfirmDiscardIfDirtyAsync())
            return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Sketch",
            AllowMultiple = false,
            FileTypeFilter = [SketchJsonType, FilePickerFileTypes.All]
        });
        if (files.Count == 0)
            return;

        var path = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            SetStatus("Could not open file.");
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path);
            var doc = SketchJson.Deserialize(json);
            _suppressDirty = true;
            try
            {
                _sketch.Document = doc;
                _documentPath = path;
                _isDirty = false;
            }
            finally
            {
                _suppressDirty = false;
            }

            SyncUiFromDocument();
            RefreshTitle();
            SetStatus($"Opened {Path.GetFileName(path)} ({doc.Elements.Count} strokes).");
        }
        catch (Exception ex)
        {
            SetStatus($"Open failed: {ex.Message}");
        }
    }

    async Task<bool> SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_documentPath))
            return await SaveAsAsync();

        return await WriteDocumentAsync(_documentPath);
    }

    async Task<bool> SaveAsAsync()
    {
        var suggested = string.IsNullOrWhiteSpace(_documentPath)
            ? "untitled.sketchjson"
            : Path.GetFileName(_documentPath);

        var result = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Sketch As",
            SuggestedFileName = suggested,
            DefaultExtension = "sketchjson",
            FileTypeChoices = [SketchJsonType]
        });
        var path = result?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
            return false;

        if (!path.EndsWith(".sketchjson", StringComparison.OrdinalIgnoreCase))
            path += ".sketchjson";

        return await WriteDocumentAsync(path);
    }

    async Task<bool> WriteDocumentAsync(string path)
    {
        var doc = _sketch.Document;
        if (doc is null)
            return false;

        try
        {
            // Keep control grid props in the document before serialize.
            doc.Grid.Size = _sketch.GridSize;
            doc.Grid.Visible = _sketch.GridVisible;
            doc.Grid.SnapEnabled = _sketch.SnapEnabled;

            var json = SketchJson.Serialize(doc);
            await File.WriteAllTextAsync(path, json);
            _documentPath = path;
            _isDirty = false;
            RefreshTitle();
            SetStatus($"Saved {Path.GetFileName(path)}.");
            return true;
        }
        catch (Exception ex)
        {
            SetStatus($"Save failed: {ex.Message}");
            return false;
        }
    }

    void SyncUiFromDocument()
    {
        var doc = _sketch.Document;
        if (doc is null)
            return;

        _suppressDirty = true;
        try
        {
            _gridSlider.Value = doc.Grid.Size;
            _sketch.GridSize = doc.Grid.Size;
            _sketch.GridVisible = doc.Grid.Visible;
            _sketch.SnapEnabled = doc.Grid.SnapEnabled;
            _gridBox.IsChecked = doc.Grid.Visible;
            _snapBox.IsChecked = doc.Grid.SnapEnabled;
        }
        finally
        {
            _suppressDirty = false;
        }
    }

    async Task<bool> ConfirmDiscardIfDirtyAsync()
    {
        if (!_isDirty)
            return true;

        var choice = await ChoiceDialog.ShowAsync(
            this,
            "Unsaved changes",
            "Save changes before continuing?",
            string.IsNullOrWhiteSpace(_documentPath) ? "Untitled sketch" : Path.GetFileName(_documentPath),
            [
                new ChoiceOption("save", "Save", IsDefault: true),
                new ChoiceOption("discard", "Don't Save"),
                new ChoiceOption("cancel", "Cancel", IsCancel: true),
            ]);

        return choice switch
        {
            "save" => await SaveAsync(),
            "discard" => true,
            _ => false
        };
    }

    async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeConfirmed || !_isDirty)
            return;

        e.Cancel = true;
        if (!await ConfirmDiscardIfDirtyAsync())
            return;

        _closeConfirmed = true;
        Close();
    }

    async Task CopyPngAsync()
    {
        var doc = _sketch.Document;
        if (doc is null)
            return;
        var clipboard = GetClipboard();
        if (clipboard is null)
        {
            SetStatus("No clipboard.");
            return;
        }

        try
        {
            var png = SketchExport.ToPng(doc, opaqueBackground: true);
            await using var stream = new MemoryStream(png);
            var bitmap = new Bitmap(stream);
            var item = new DataTransferItem();
            item.SetBitmap(bitmap);
            item.Set(DataFormat.CreateBytesPlatformFormat("PNG"), png);
            item.Set(DataFormat.CreateBytesPlatformFormat("image/png"), png);
            var data = new DataTransfer();
            data.Add(item);
            await clipboard.SetDataAsync(data);
            SetStatus($"Copied PNG ({png.Length:N0} bytes).");
        }
        catch (Exception ex)
        {
            SetStatus($"PNG copy failed: {ex.Message}");
        }
    }

    async Task CopySvgAsync()
    {
        var doc = _sketch.Document;
        if (doc is null)
            return;
        var clipboard = GetClipboard();
        if (clipboard is null)
        {
            SetStatus("No clipboard.");
            return;
        }

        try
        {
            var svg = SketchExport.ToSvg(doc);
            await clipboard.SetTextAsync(svg);
            SetStatus($"Copied SVG ({svg.Length:N0} chars).");
        }
        catch (Exception ex)
        {
            SetStatus($"SVG copy failed: {ex.Message}");
        }
    }

    IClipboard? GetClipboard() => TopLevel.GetTopLevel(this)?.Clipboard;

    void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if (ctrl && shift && e.Key == Key.S)
        {
            _ = SaveAsAsync();
            e.Handled = true;
            return;
        }

        if (ctrl)
        {
            switch (e.Key)
            {
                case Key.N:
                    _ = NewAsync();
                    e.Handled = true;
                    return;
                case Key.O:
                    _ = OpenAsync();
                    e.Handled = true;
                    return;
                case Key.S:
                    _ = SaveAsync();
                    e.Handled = true;
                    return;
                case Key.Z:
                    _sketch.Undo();
                    e.Handled = true;
                    return;
                case Key.Y:
                    _sketch.Redo();
                    e.Handled = true;
                    return;
            }
        }

        if (ctrl || shift)
            return;

        switch (e.Key)
        {
            case Key.P:
                SelectTool(SketchTool.Pen);
                e.Handled = true;
                break;
            case Key.L:
                SelectTool(SketchTool.Line);
                e.Handled = true;
                break;
            case Key.S:
                SelectTool(SketchTool.Spline);
                e.Handled = true;
                break;
            case Key.R:
                SelectTool(SketchTool.Rect);
                e.Handled = true;
                break;
            case Key.C:
                SelectTool(SketchTool.Ellipse);
                e.Handled = true;
                break;
            case Key.E:
                SelectTool(SketchTool.Eraser);
                e.Handled = true;
                break;
            case Key.V:
                SelectTool(SketchTool.Select);
                e.Handled = true;
                break;
            case Key.Delete:
                _sketch.Document?.DeleteSelection();
                e.Handled = true;
                break;
        }
    }

    void RefreshStatus()
    {
        var doc = _sketch.Document;
        if (doc is null)
        {
            _status.Text = "";
            return;
        }

        _status.Text =
            $"{_sketch.Tool} · {_sketch.StrokeStyle} · {doc.Elements.Count} · sel {doc.Selection.Count} · {_sketch.StrokeColor} · w{_sketch.StrokeWidth:0.##}"
            + (_sketch.FillEnabled ? " · fill" : "");
    }

    void SetStatus(string text) => _status.Text = text;
}
