using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Novolis.Avalonia.Agent;
using Novolis.Avalonia.Agent.Protocol;
using Novolis.Avalonia.Controls;
using Novolis.Avalonia.Controls.Sketch;
using Optris.Icons.Avalonia;

namespace SketchStudio;

internal sealed class MainWindow : Window
{
    // 13 opaque standards — custom color for #RRGGBB / #AARRGGBB via popup.
    static readonly string[] Palette =
    [
        "#000000", "#1e1e1e", "#ffffff", "#adb5bd", "#264653",
        "#e63946", "#fb5607", "#e9c46a", "#2a9d8f", "#457b9d",
        "#3a86ff", "#8338ec", "#ff006e"
    ];

    static readonly FilePickerFileType SketchJsonType = new("Sketch JSON")
    {
        Patterns = ["*.sketchjson"]
    };

    static readonly FilePickerFileType PngType = new("PNG image")
    {
        Patterns = ["*.png"]
    };

    static readonly FilePickerFileType SvgType = new("SVG image")
    {
        Patterns = ["*.svg"]
    };

    readonly SketchStudioSettings _settings;
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
    readonly MenuFlyout _recentFlyout = new();
    readonly Button _recentButton;
    readonly ComboBox _layerCombo;
    bool _suppressLayerUi;

    string? _documentPath;
    bool _isDirty;
    bool _suppressDirty;
    bool _closeConfirmed;

    public MainWindow(SketchStudioSettings settings)
    {
        _settings = settings;
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

        _snapBox = Toggle(
            "Snap to grid",
            true,
            v => _sketch.SnapEnabled = v,
            SketchShortcuts.FormatTip("Snap to grid", "—", "Quantize pointer samples to the grid while drawing."));
        _meetupBox = Toggle(
            "Meetup",
            true,
            v => _sketch.MeetupEnabled = v,
            SketchShortcuts.FormatTip(
                "Meetup",
                "—",
                "Snap endpoints to nearby vertices (Line/Spline/shapes). Disabled mid-stroke for Pen freehand."));
        _gridBox = Toggle(
            "Grid",
            true,
            v => _sketch.GridVisible = v,
            SketchShortcuts.FormatTip("Grid", "—", "Show or hide the background grid."));
        _fillBox = Toggle(
            "Fill",
            false,
            v =>
            {
                _sketch.FillEnabled = v;
                RefreshStatus();
            },
            SketchShortcuts.FormatTip("Fill", "—", "New closed shapes use the current color as fill."));

        _gridSlider = new Slider
        {
            Minimum = 5,
            Maximum = 80,
            Value = 20,
            Width = 100,
            VerticalAlignment = VerticalAlignment.Center
        };
        SketchShortcuts.ApplyTip(_gridSlider, SketchShortcuts.FormatTip("Grid size", "—", "Spacing of snap/grid units (5–80)."));
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
        SketchShortcuts.ApplyTip(_widthSlider, SketchShortcuts.FormatTip("Stroke width", "—", "Pen / outline thickness (0.25–16)."));
        _widthSlider.PropertyChanged += (_, e) =>
        {
            if (e.Property == RangeBase.ValueProperty)
            {
                _sketch.StrokeWidth = _widthSlider.Value;
                RefreshStatus();
            }
        };

        SketchShortcuts.ApplyTip(
            _colorPreview,
            SketchShortcuts.FormatTip("Current color", "—", "Active stroke and fill color. Use a swatch or Custom for hex/RGBA."));

        _recentButton = new Button
        {
            Width = 36,
            Height = 32,
            Padding = new Thickness(0),
            Content = new Icon { Value = "fa-solid fa-clock-rotate-left", FontSize = 14 },
            Flyout = _recentFlyout
        };
        SketchShortcuts.ApplyTip(
            _recentButton,
            SketchShortcuts.FormatTip("Recent", "—", "Reopen from the last 8 sketches (stored under LocalAppData)."));
        _recentFlyout.Opening += (_, _) => RebuildRecentFlyout();

        var file = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Children =
            {
                IconButton("fa-solid fa-file", SketchShortcuts.FormatTip("New", "Ctrl+N", "Start a blank sketch; prompts if unsaved."), () => _ = NewAsync(), "sketch.file.new"),
                IconButton("fa-solid fa-folder-open", SketchShortcuts.FormatTip("Open", "Ctrl+O", "Open a .sketchjson document."), () => _ = OpenAsync(), "sketch.file.open"),
                _recentButton,
                IconButton("fa-solid fa-floppy-disk", SketchShortcuts.FormatTip("Save", "Ctrl+S", "Write the current .sketchjson path."), () => _ = SaveAsync(), "sketch.file.save"),
                IconButton("fa-solid fa-file-export", SketchShortcuts.FormatTip("Save As", "Ctrl+Shift+S", "Choose a new .sketchjson path."), () => _ = SaveAsAsync(), "sketch.file.saveAs"),
            }
        };

        var tools = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Children =
            {
                ToolBtn("fa-solid fa-pen", SketchShortcuts.FormatTip("Pen", "P", "Freehand stroke. Meetup is off while dragging."), SketchTool.Pen, selected: true, agentId: "sketch.tool.pen"),
                ToolBtn("fa-solid fa-slash", SketchShortcuts.FormatTip("Line", "L", "Click vertices. Enter finishes; Ctrl+Enter closes."), SketchTool.Line, agentId: "sketch.tool.line"),
                ToolBtn("fa-solid fa-bezier-curve", SketchShortcuts.FormatTip("Spline", "S", "Click control points. Enter / Ctrl+Enter like Line."), SketchTool.Spline, agentId: "sketch.tool.spline"),
                ToolBtn("fa-regular fa-square", SketchShortcuts.FormatTip("Box", "R", "Drag an axis-aligned rectangle."), SketchTool.Rect, agentId: "sketch.tool.rect"),
                ToolBtn("fa-regular fa-circle", SketchShortcuts.FormatTip("Circle", "C", "Drag ellipse; hold Shift for a circle."), SketchTool.Ellipse, agentId: "sketch.tool.ellipse"),
                ToolBtn("fa-solid fa-comment", SketchShortcuts.FormatTip("Speech bubble", "B", "Drag a rounded bubble with a tail."), SketchTool.SpeechBubble, agentId: "sketch.tool.speech"),
                ToolBtn("fa-solid fa-font", SketchShortcuts.FormatTip("Text", "T", "Click to place a text label."), SketchTool.Text, agentId: "sketch.tool.text"),
                ToolBtn("fa-solid fa-i-cursor", SketchShortcuts.FormatTip("Text box", "X", "Drag a bordered text box."), SketchTool.TextBox, agentId: "sketch.tool.textbox"),
                ToolBtn("fa-solid fa-eraser", SketchShortcuts.FormatTip("Eraser", "E", "Click or drag over strokes to erase."), SketchTool.Eraser, agentId: "sketch.tool.eraser"),
                ToolBtn("fa-solid fa-fill-drip", SketchShortcuts.FormatTip("Paint bucket", "K", "Fill a closed shape, or flood an enclosed pocket between strokes."), SketchTool.Fill, agentId: "sketch.tool.fill"),
                ToolBtn("fa-solid fa-mouse-pointer", SketchShortcuts.FormatTip("Select", "V", "Move, resize, rotate grip; Shift multi-select."), SketchTool.Select, agentId: "sketch.tool.select"),
                Sep(),
                IconButton(
                    "fa-solid fa-check",
                    SketchShortcuts.FormatTip("Complete", "Enter", "Commit an open line or spline."),
                    () =>
                    {
                        _sketch.CompleteDrawing(closeShape: false);
                        SetStatus(_sketch.HasInProgressDrawing ? "Still drawing…" : "Completed.");
                    },
                    "sketch.action.complete"),
                IconButton(
                    "fa-solid fa-draw-polygon",
                    SketchShortcuts.FormatTip("Close shape", "Ctrl+Enter", "Close with ≥3 points, or click the start vertex."),
                    () =>
                    {
                        _sketch.CompleteDrawing(closeShape: true);
                        SetStatus(_sketch.HasInProgressDrawing ? "Need ≥3 points to close." : "Closed.");
                    },
                    "sketch.action.close"),
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
            SketchShortcuts.ApplyTip(
                btn,
                SketchShortcuts.FormatTip("Color", "—", $"Apply {swatch} to stroke and fill."));
            btn.Click += (_, _) => SetStrokeColor(swatch);
            colors.Children.Add(btn);
        }

        var customBtn = new Button
        {
            Height = 22,
            Padding = new Thickness(8, 0),
            Content = new TextBlock
            {
                Text = "Custom…",
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            },
            VerticalAlignment = VerticalAlignment.Center
        };
        SketchShortcuts.ApplyTip(
            customBtn,
            SketchShortcuts.FormatTip("Custom color", "—", "Open hex / RGBA editor (#RRGGBB or #AARRGGBB)."));
        AgentProperties.SetId(customBtn, "sketch.color.custom", AgentRoleNames.Button);
        customBtn.Click += (_, _) => _ = ShowCustomColorAsync();
        colors.Children.Add(customBtn);

        var styles = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        styles.Children.Add(StyleBtn("────", SketchShortcuts.FormatTip("Solid", "—", "Continuous stroke."), SketchStrokeStyle.Solid, selected: true));
        styles.Children.Add(StyleBtn("- - -", SketchShortcuts.FormatTip("Dashed", "—", "Dashed outline."), SketchStrokeStyle.Dashed));
        styles.Children.Add(StyleBtn("····", SketchShortcuts.FormatTip("Dotted", "—", "Dotted outline."), SketchStrokeStyle.Dotted));
        styles.Children.Add(StyleBtn("-·-", SketchShortcuts.FormatTip("Dash-dot", "—", "Dash-dot outline."), SketchStrokeStyle.DashDot));
        styles.Children.Add(StyleBtn("·····", SketchShortcuts.FormatTip("Stipple", "—", "Fine stipple outline."), SketchStrokeStyle.Stipple));

        _layerCombo = new ComboBox
        {
            MinWidth = 140,
            Height = 28,
            VerticalAlignment = VerticalAlignment.Center
        };
        SketchShortcuts.ApplyTip(
            _layerCombo,
            SketchShortcuts.FormatTip("Active layer", "—", "New shapes go on this layer. Hidden layers are not drawn or hit-tested."));
        _layerCombo.SelectionChanged += (_, _) =>
        {
            if (_suppressLayerUi || _layerCombo.SelectedItem is not LayerItem item)
                return;
            var doc = _sketch.Document;
            if (doc is null)
                return;
            doc.ActiveLayerId = item.Id;
            SetStatus($"Active layer: {item.Name}");
        };

        var layers = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Children =
            {
                Label("Layer"),
                _layerCombo,
                IconButton(
                    "fa-solid fa-plus",
                    SketchShortcuts.FormatTip("Add layer", "—", "Create a new layer and make it active."),
                    () =>
                    {
                        var doc = _sketch.Document ?? new SketchDocument();
                        _sketch.Document ??= doc;
                        var layer = doc.AddLayer();
                        RefreshLayerUi();
                        SetStatus($"Added {layer.Name}.");
                    },
                    "sketch.layer.add"),
                IconButton(
                    "fa-solid fa-eye",
                    SketchShortcuts.FormatTip("Toggle visibility", "—", "Show or hide the active layer."),
                    () =>
                    {
                        var doc = _sketch.Document;
                        if (doc is null)
                            return;
                        var layer = doc.FindLayer(doc.ActiveLayerId);
                        if (layer is null)
                            return;
                        doc.SetLayerVisible(layer.Id, !layer.Visible);
                        RefreshLayerUi();
                        SetStatus(layer.Visible ? $"Shown {layer.Name}." : $"Hidden {layer.Name}.");
                    },
                    "sketch.layer.visibility"),
                IconButton(
                    "fa-solid fa-lock",
                    SketchShortcuts.FormatTip("Toggle lock", "—", "Lock or unlock the active layer (locked layers cannot be edited or filled)."),
                    () =>
                    {
                        var doc = _sketch.Document;
                        if (doc is null)
                            return;
                        var layer = doc.FindLayer(doc.ActiveLayerId);
                        if (layer is null)
                            return;
                        doc.SetLayerLocked(layer.Id, !layer.Locked);
                        RefreshLayerUi();
                        SetStatus(layer.Locked ? $"Locked {layer.Name}." : $"Unlocked {layer.Name}.");
                    },
                    "sketch.layer.lock"),
            }
        };

        var editActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                IconButton(
                    "fa-solid fa-object-group",
                    SketchShortcuts.FormatTip("Fuse", "Ctrl+G", "Group ≥2 selected shapes so they transform together."),
                    () => SetStatus(_sketch.FuseSelection() ? "Fused." : "Select ≥2 shapes to fuse.")),
                IconButton(
                    "fa-solid fa-object-ungroup",
                    SketchShortcuts.FormatTip("Ungroup", "Ctrl+Shift+G", "Clear groupId on the selection."),
                    () => SetStatus(_sketch.UngroupSelection() ? "Ungrouped." : "Nothing to ungroup.")),
                IconButton(
                    "fa-solid fa-table-cells",
                    SketchShortcuts.FormatTip("Gridify", "—", "Snap selected geometry onto the current grid."),
                    () =>
                    {
                        _sketch.GridifySelection();
                        SetStatus("Gridified.");
                    }),
                IconButton("fa-solid fa-rotate-left", SketchShortcuts.FormatTip("Undo", "Ctrl+Z", "Undo the last document change."), () => _sketch.Undo()),
                IconButton("fa-solid fa-rotate-right", SketchShortcuts.FormatTip("Redo", "Ctrl+Y", "Redo."), () => _sketch.Redo()),
                IconButton(
                    "fa-solid fa-trash",
                    SketchShortcuts.FormatTip("Clear", "—", "Remove every element (undoable)."),
                    () =>
                    {
                        _sketch.Clear();
                        SetStatus("Cleared.");
                    }),
                IconButton(
                    "fa-solid fa-paste",
                    SketchShortcuts.FormatTip("Paste image", "Ctrl+V", "Insert a clipboard bitmap at the viewport center."),
                    () => _ = PasteImageAsync()),
            }
        };

        var exportActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                IconButton(
                    "fa-solid fa-image",
                    SketchShortcuts.FormatTip("Copy PNG", "—", "Copy opaque white-background PNG to the clipboard."),
                    () => _ = CopyPngAsync(),
                    "sketch.export.copyPng"),
                IconButton(
                    "fa-solid fa-file-image",
                    SketchShortcuts.FormatTip("Save PNG file", "—", "Write an opaque PNG file to disk."),
                    () => _ = SavePngAsync(),
                    "sketch.export.savePng"),
                IconButton(
                    "fa-solid fa-code",
                    SketchShortcuts.FormatTip("Copy SVG", "—", "Copy SVG markup to the clipboard as text."),
                    () => _ = CopySvgAsync(),
                    "sketch.export.copySvg"),
                IconButton(
                    "fa-solid fa-file-code",
                    SketchShortcuts.FormatTip("Save SVG file", "—", "Write an SVG file to disk."),
                    () => _ = SaveSvgAsync(),
                    "sketch.export.saveSvg"),
                IconButton(
                    "fa-solid fa-circle-question",
                    SketchShortcuts.FormatTip("Shortcuts", "F1", "Open the full keyboard shortcut reference."),
                    () => _ = SketchShortcuts.ShowHelpAsync(this),
                    "sketch.help.shortcuts"),
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
            }
        };

        var row2 = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Margin = new Thickness(10, 0, 10, 4),
            Children =
            {
                _snapBox,
                _meetupBox,
                _gridBox,
                _fillBox,
                Label("Grid"),
                _gridSlider,
                Label("Width"),
                _widthSlider,
                Sep(),
                Label("Color"),
                colors,
            }
        };

        var row3 = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Margin = new Thickness(10, 0, 10, 4),
            Children =
            {
                Label("Stroke"),
                styles,
                Sep(),
                layers,
                Sep(),
                editActions,
                Sep(),
                exportActions,
            }
        };

        var hint = new TextBlock
        {
            Text = "Hover for tips · F1 shortcuts · K paint-bucket · layers on row 3 · Save PNG/SVG write files · Space+drag pan",
            Opacity = 0.6,
            FontSize = 11,
            Margin = new Thickness(12, 0, 12, 6),
            TextWrapping = TextWrapping.Wrap
        };
        SketchShortcuts.ApplyTip(
            hint,
            SketchShortcuts.FormatTip("Hint strip", "F1", "Press F1 for the complete shortcut reference."));

        var top = new StackPanel { Children = { row1, row2, row3, hint } };

        _status.Margin = new Thickness(12, 4);
        _status.VerticalAlignment = VerticalAlignment.Center;
        var statusBar = new Border
        {
            BorderThickness = new Thickness(0, 1, 0, 0),
            BorderBrush = Brushes.Gray,
            Opacity = 0.9,
            Child = _status,
            MinHeight = 28,
            VerticalAlignment = VerticalAlignment.Center
        };

        var root = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        DockPanel.SetDock(statusBar, Dock.Bottom);
        root.Children.Add(top);
        root.Children.Add(statusBar);
        root.Children.Add(_sketch);
        Content = root;

        AgentProperties.SetId(this, "sketch.window", AgentRoleNames.Window);
        AgentProperties.SetId(_sketch, "sketch.viewport");
        AgentProperties.SetId(_status, "sketch.status");
        AgentProperties.SetId(_layerCombo, "sketch.layers", AgentRoleNames.ComboBox);
        AgentProperties.SetId(_recentButton, "sketch.file.recent", AgentRoleNames.Button);
        AgentProperties.SetId(_snapBox, "sketch.toggle.snap", AgentRoleNames.CheckBox);
        AgentProperties.SetId(_meetupBox, "sketch.toggle.meetup", AgentRoleNames.CheckBox);
        AgentProperties.SetId(_gridBox, "sketch.toggle.grid", AgentRoleNames.CheckBox);
        AgentProperties.SetId(_fillBox, "sketch.toggle.fill", AgentRoleNames.CheckBox);

        KeyDown += OnKeyDown;
        Closing += OnClosing;
        Opened += OnOpened;
        RefreshLayerUi();
        RefreshTitle();
        RefreshStatus();
    }

    async void OnOpened(object? sender, EventArgs e)
    {
        Opened -= OnOpened;
        var path = _settings.LastDocumentPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        await LoadDocumentFromPathAsync(path, remember: false);
    }

    void RebuildRecentFlyout()
    {
        _recentFlyout.Items.Clear();
        var paths = _settings.RecentPaths.Where(File.Exists).ToList();
        if (paths.Count == 0)
        {
            _recentFlyout.Items.Add(new Avalonia.Controls.MenuItem
            {
                Header = "(no recent sketches)",
                IsEnabled = false
            });
            return;
        }

        foreach (var path in paths)
        {
            var local = path;
            var item = new Avalonia.Controls.MenuItem { Header = Path.GetFileName(local), Tag = local };
            SketchShortcuts.ApplyTip(item, local);
            item.Click += (_, _) => _ = OpenRecentAsync(local);
            _recentFlyout.Items.Add(item);
        }
    }

    async Task OpenRecentAsync(string path)
    {
        if (!await ConfirmDiscardIfDirtyAsync())
            return;
        await LoadDocumentFromPathAsync(path, remember: true);
    }

    void OnSketchDocumentChanged()
    {
        if (!_suppressDirty)
            _isDirty = true;
        RefreshTitle();
        RefreshStatus();
        RefreshLayerUi();
    }

    void RefreshLayerUi()
    {
        var doc = _sketch.Document;
        if (doc is null)
            return;

        doc.EnsureDefaultLayer();
        _suppressLayerUi = true;
        try
        {
            var selected = doc.ActiveLayerId;
            _layerCombo.Items.Clear();
            foreach (var layer in doc.Layers)
            {
                var label = layer.Name;
                if (!layer.Visible)
                    label += " (hidden)";
                if (layer.Locked)
                    label += " (locked)";
                var item = new LayerItem(layer.Id, label, layer.Name);
                _layerCombo.Items.Add(item);
                if (string.Equals(layer.Id, selected, StringComparison.Ordinal))
                    _layerCombo.SelectedItem = item;
            }

            if (_layerCombo.SelectedItem is null && _layerCombo.Items.Count > 0)
                _layerCombo.SelectedIndex = 0;
        }
        finally
        {
            _suppressLayerUi = false;
        }
    }

    sealed record LayerItem(string Id, string Display, string Name)
    {
        public override string ToString() => Display;
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

    ToggleButton ToolBtn(string icon, string tip, SketchTool tool, bool selected = false, string? agentId = null)
    {
        var btn = new ToggleButton
        {
            Width = 36,
            Height = 32,
            IsChecked = selected,
            Content = new Icon { Value = icon, FontSize = 14 }
        };
        SketchShortcuts.ApplyTip(btn, tip);
        if (!string.IsNullOrWhiteSpace(agentId))
            AgentProperties.SetId(btn, agentId, AgentRoleNames.Toggle);
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
        SketchShortcuts.ApplyTip(btn, tip);
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

    static Button IconButton(string icon, string tip, Action action, string? agentId = null)
    {
        var b = new Button
        {
            Width = 36,
            Height = 32,
            Content = new Icon { Value = icon, FontSize = 14 },
            Padding = new Thickness(0)
        };
        SketchShortcuts.ApplyTip(b, tip);
        if (!string.IsNullOrWhiteSpace(agentId))
            AgentProperties.SetId(b, agentId, AgentRoleNames.Button);
        b.Click += (_, _) => action();
        return b;
    }

    static CheckBox Toggle(string label, bool on, Action<bool> set, string tip)
    {
        var box = new CheckBox
        {
            Content = label,
            IsChecked = on,
            VerticalAlignment = VerticalAlignment.Center
        };
        SketchShortcuts.ApplyTip(box, tip);
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

        await LoadDocumentFromPathAsync(path, remember: true);
    }

    async Task LoadDocumentFromPathAsync(string path, bool remember)
    {
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

            if (remember)
                _settings.RememberDocument(path);

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
            _settings.RememberDocument(path);
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

        RefreshLayerUi();
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

    async Task SavePngAsync()
    {
        var doc = _sketch.Document;
        if (doc is null)
            return;

        var suggested = string.IsNullOrWhiteSpace(_documentPath)
            ? "untitled.png"
            : Path.ChangeExtension(Path.GetFileName(_documentPath), ".png");

        var result = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save PNG As",
            SuggestedFileName = suggested,
            DefaultExtension = "png",
            FileTypeChoices = [PngType]
        });
        var path = result?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
            return;

        if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            path += ".png";

        try
        {
            var png = SketchExport.ToPng(doc, opaqueBackground: true);
            await File.WriteAllBytesAsync(path, png);
            SetStatus($"Saved PNG {Path.GetFileName(path)} ({png.Length:N0} bytes).");
        }
        catch (Exception ex)
        {
            SetStatus($"PNG save failed: {ex.Message}");
        }
    }

    async Task SaveSvgAsync()
    {
        var doc = _sketch.Document;
        if (doc is null)
            return;

        var suggested = string.IsNullOrWhiteSpace(_documentPath)
            ? "untitled.svg"
            : Path.ChangeExtension(Path.GetFileName(_documentPath), ".svg");

        var result = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save SVG As",
            SuggestedFileName = suggested,
            DefaultExtension = "svg",
            FileTypeChoices = [SvgType]
        });
        var path = result?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
            return;

        if (!path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            path += ".svg";

        try
        {
            var svg = SketchExport.ToSvg(doc);
            await File.WriteAllTextAsync(path, svg);
            SetStatus($"Saved SVG {Path.GetFileName(path)} ({svg.Length:N0} chars).");
        }
        catch (Exception ex)
        {
            SetStatus($"SVG save failed: {ex.Message}");
        }
    }

    IClipboard? GetClipboard() => TopLevel.GetTopLevel(this)?.Clipboard;

    void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if (e.Key == Key.F1)
        {
            _ = SketchShortcuts.ShowHelpAsync(this);
            e.Handled = true;
            return;
        }

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
                case Key.V:
                    _ = PasteImageAsync();
                    e.Handled = true;
                    return;
                case Key.G:
                    if (shift)
                        SetStatus(_sketch.UngroupSelection() ? "Ungrouped." : "Nothing to ungroup.");
                    else
                        SetStatus(_sketch.FuseSelection() ? "Fused." : "Select ≥2 shapes to fuse.");
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
            case Key.B:
                SelectTool(SketchTool.SpeechBubble);
                e.Handled = true;
                break;
            case Key.T:
                SelectTool(SketchTool.Text);
                e.Handled = true;
                break;
            case Key.X:
                SelectTool(SketchTool.TextBox);
                e.Handled = true;
                break;
            case Key.E:
                SelectTool(SketchTool.Eraser);
                e.Handled = true;
                break;
            case Key.K:
                SelectTool(SketchTool.Fill);
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

    async Task PasteImageAsync()
    {
        var clipboard = GetClipboard();
        if (clipboard is null)
        {
            SetStatus("No clipboard.");
            return;
        }

        try
        {
            var bitmap = await clipboard.TryGetBitmapAsync();
            if (bitmap is null)
            {
                SetStatus("Clipboard has no image.");
                return;
            }

            using (bitmap)
            {
                using var ms = new MemoryStream();
                bitmap.Save(ms);
                var placed = _sketch.PasteImage(ms.ToArray(), _sketch.ViewportCenterWorld());
                SetStatus(placed is null ? "Could not decode clipboard image." : "Pasted image.");
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Paste failed: {ex.Message}");
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
