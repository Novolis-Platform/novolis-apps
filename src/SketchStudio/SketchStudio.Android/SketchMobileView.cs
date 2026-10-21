using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Novolis.Avalonia.Controls.Sketch;
using Novolis.Avalonia.GraphicalProfile;
using AvaloniaButton = Avalonia.Controls.Button;
using AvaloniaCheckBox = Avalonia.Controls.CheckBox;
using AvaloniaOrientation = Avalonia.Layout.Orientation;

namespace SketchStudio.Android;

/// <summary>
/// Touch-first Sketch Studio surface. The document and command semantics match
/// the wide host; this view places secondary commands in bottom sheets.
/// </summary>
internal sealed class SketchMobileView : UserControl
{
    private static readonly string[] Palette =
    [
        "#000000", "#1e1e1e", "#ffffff", "#adb5bd", "#264653",
        "#e63946", "#fb5607", "#e9c46a", "#2a9d8f", "#457b9d",
        "#3a86ff", "#8338ec", "#ff006e",
    ];

    private static readonly FilePickerFileType SketchJsonType = new("Sketch JSON")
    {
        Patterns = ["*.sketchjson"],
        MimeTypes = ["application/json", "application/octet-stream"],
    };

    private static readonly FilePickerFileType PngType = new("PNG image")
    {
        Patterns = ["*.png"],
        MimeTypes = ["image/png"],
    };

    private static readonly FilePickerFileType SvgType = new("SVG image")
    {
        Patterns = ["*.svg"],
        MimeTypes = ["image/svg+xml"],
    };

    private readonly SketchMobileSettings _settings;
    private readonly SketchControl _sketch;
    private readonly TextBlock _title;
    private readonly TextBlock _status;
    private readonly AvaloniaButton _saveButton;
    private readonly Grid _bottomBar;
    private readonly Grid _sheetLayer;
    private readonly TextBlock _sheetTitle;
    private readonly ContentControl _sheetContent;

    private CancellationTokenSource? _saveDebounce;
    private TaskCompletionSource<string?>? _choiceCompletion;
    private IStorageFile? _sourceFile;
    private string? _workingPath;
    private string _documentName = "Untitled";
    private bool _isDirty;
    private bool _suppressDirty;
    private bool _hasLoaded;

    public SketchMobileView()
    {
        _settings = new SketchMobileSettings();
        _workingPath = _settings.CreateWorkingPath("untitled");

        _sketch = new SketchControl
        {
            Tool = SketchTool.Pen,
            GridSize = 20,
            GridVisible = true,
            SnapEnabled = true,
            MeetupEnabled = true,
            StrokeColor = "#1e1e1e",
            FillColor = "#1e1e1e",
            StrokeWidth = 2,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Document = NewDocument(),
        };
        _sketch.DocumentChanged += OnDocumentChanged;
        _sketch.SelectionChanged += RefreshUi;

        _title = new TextBlock
        {
            Foreground = GraphicalProfile.TextBrush,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _status = new TextBlock
        {
            Foreground = GraphicalProfile.MutedBrush,
            FontSize = 11,
            Margin = new Thickness(12, 5),
            TextWrapping = TextWrapping.Wrap,
        };
        _saveButton = CreateButton("Save", () => _ = SaveAsync());
        _bottomBar = new Grid
        {
            Background = GraphicalProfile.SurfaceBrush,
            ColumnSpacing = 4,
            Margin = new Thickness(6, 4),
        };
        _sheetTitle = new TextBlock
        {
            Foreground = GraphicalProfile.TextBrush,
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _sheetContent = new ContentControl
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
        };
        _sheetLayer = new Grid { IsVisible = false };

        Content = BuildRoot();
        AttachedToVisualTree += OnAttachedToVisualTree;
    }

    private Control BuildRoot()
    {
        var topBar = BuildTopBar();
        var statusBar = new Border
        {
            Background = GraphicalProfile.SurfaceBrush,
            BorderBrush = GraphicalProfile.BorderBrush,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = _status,
        };

        BuildSheetLayer();

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto"),
            Background = GraphicalProfile.BackgroundBrush,
        };
        root.Children.Add(topBar);
        Grid.SetRow(_sketch, 1);
        root.Children.Add(_sketch);
        Grid.SetRow(statusBar, 2);
        root.Children.Add(statusBar);
        Grid.SetRow(_bottomBar, 3);
        root.Children.Add(_bottomBar);
        Grid.SetRowSpan(_sheetLayer, 4);
        root.Children.Add(_sheetLayer);

        RebuildBottomBar();
        RefreshUi();
        return root;
    }

    private Control BuildTopBar()
    {
        var titleBlock = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                _title,
                new TextBlock
                {
                    Text = "Two fingers navigate · active tool draws",
                    Foreground = GraphicalProfile.MutedBrush,
                    FontSize = 11,
                },
            },
        };

        var fileButton = CreateButton("File", ShowDocumentSheet);
        var moreButton = CreateButton("More", ShowMoreSheet);
        var bar = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto"),
            ColumnSpacing = 5,
            Background = GraphicalProfile.SurfaceBrush,
            Margin = new Thickness(8, 7),
        };
        bar.Children.Add(titleBlock);
        Grid.SetColumn(_saveButton, 1);
        Grid.SetColumn(fileButton, 2);
        Grid.SetColumn(moreButton, 3);
        bar.Children.Add(_saveButton);
        bar.Children.Add(fileButton);
        bar.Children.Add(moreButton);
        return bar;
    }

    private void BuildSheetLayer()
    {
        var scrim = new AvaloniaButton
        {
            Background = GraphicalProfile.BackgroundBrush,
            BorderThickness = new Thickness(0),
            Opacity = 0.72,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
        };
        scrim.Click += (_, _) => CloseSheet();
        _sheetLayer.Children.Add(scrim);

        var close = CreateButton("Close", CloseSheet);
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 8,
            Margin = new Thickness(16, 14, 12, 10),
        };
        header.Children.Add(_sheetTitle);
        Grid.SetColumn(close, 1);
        header.Children.Add(close);

        var dock = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(header, Dock.Top);
        dock.Children.Add(header);
        dock.Children.Add(new ScrollViewer
        {
            Content = _sheetContent,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        });

        var panel = new Border
        {
            Background = GraphicalProfile.SurfaceBrush,
            BorderBrush = GraphicalProfile.BorderBrush,
            BorderThickness = new Thickness(0, 1, 0, 0),
            CornerRadius = new CornerRadius(14, 14, 0, 0),
            MaxHeight = 620,
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = dock,
        };
        _sheetLayer.Children.Add(panel);
    }

    private void RebuildBottomBar()
    {
        _bottomBar.Children.Clear();
        _bottomBar.ColumnDefinitions.Clear();

        var actions = new List<(string Label, Action Action, bool Selected)>();
        if (_sketch.HasInProgressDrawing)
        {
            actions.Add(("Done", () =>
            {
                _sketch.CompleteDrawing();
                SetStatus("Drawing completed.");
                RefreshUi();
            }, false));
            actions.Add(("Close", () =>
            {
                _sketch.CompleteDrawing(closeShape: true);
                SetStatus("Shape closed.");
                RefreshUi();
            }, false));
            actions.Add(("Cancel", () =>
            {
                _sketch.CancelPointerInteraction();
                SetStatus("Drawing cancelled.");
                RefreshUi();
            }, false));
        }
        else
        {
            actions.Add(("Select", () => SelectTool(SketchTool.Select), _sketch.Tool == SketchTool.Select));
            actions.Add(("Pen", () => SelectTool(SketchTool.Pen), _sketch.Tool == SketchTool.Pen));
            actions.Add(("Shapes", ShowToolSheet, IsShapeTool(_sketch.Tool)));
            actions.Add(("Text", () => SelectTool(SketchTool.Text), _sketch.Tool == SketchTool.Text));
        }

        actions.Add(("Undo", () =>
        {
            _sketch.Undo();
            SetStatus("Undo.");
        }, false));
        actions.Add(("Redo", () =>
        {
            _sketch.Redo();
            SetStatus("Redo.");
        }, false));
        actions.Add(("More", ShowMoreSheet, false));

        foreach (var action in actions)
        {
            _bottomBar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var button = CreateButton(action.Label, action.Action, action.Selected);
            button.FontSize = 11;
            button.Padding = new Thickness(3, 7);
            Grid.SetColumn(button, _bottomBar.ColumnDefinitions.Count - 1);
            _bottomBar.Children.Add(button);
        }
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs args)
    {
        if (_hasLoaded)
            return;

        _hasLoaded = true;
        _ = LoadInitialAsync();
    }

    private async Task LoadInitialAsync()
    {
        var path = _settings.LastWorkingPath;
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            var recent = _settings.Recent.FirstOrDefault(item =>
                string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase));
            await LoadWorkingCopyAsync(path, recent?.Name ?? Path.GetFileName(path));
            return;
        }

        SetStatus("Ready · start drawing or open a sketch.");
    }

    private async Task LoadWorkingCopyAsync(string path, string name)
    {
        try
        {
            var json = await File.ReadAllTextAsync(path);
            var document = SketchJson.Deserialize(json);
            ApplyDocument(document);
            _workingPath = path;
            _sourceFile = null;
            _documentName = string.IsNullOrWhiteSpace(name) ? "Untitled" : name;
            _isDirty = false;
            _settings.Remember(path, _documentName);
            RefreshUi();
            SetStatus($"Opened {_documentName}.");
        }
        catch (Exception exception)
        {
            SetStatus($"Could not open working copy: {exception.Message}");
        }
    }

    private async Task NewAsync()
    {
        if (!await ConfirmDiscardIfDirtyAsync())
            return;

        ApplyDocument(NewDocument(_sketch.GridSize, _sketch.GridVisible, _sketch.SnapEnabled));
        _workingPath = _settings.CreateWorkingPath("untitled");
        _sourceFile = null;
        _documentName = "Untitled";
        _isDirty = false;
        CloseSheet();
        RefreshUi();
        SetStatus("New sketch.");
    }

    private async Task OpenAsync()
    {
        if (!await ConfirmDiscardIfDirtyAsync())
            return;

        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is not { CanOpen: true })
        {
            SetStatus("This host cannot open files.");
            return;
        }

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Sketch",
            AllowMultiple = false,
            FileTypeFilter = [SketchJsonType],
        });
        if (files.Count == 0)
            return;

        await OpenStorageFileAsync(files[0]);
    }

    private async Task OpenStorageFileAsync(IStorageFile file)
    {
        try
        {
            await using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();
            var document = SketchJson.Deserialize(json);
            var workingPath = _settings.CreateWorkingPath(file.Name);
            await File.WriteAllTextAsync(workingPath, json);

            ApplyDocument(document);
            _workingPath = workingPath;
            _sourceFile = file;
            _documentName = string.IsNullOrWhiteSpace(file.Name) ? "Sketch" : file.Name;
            _isDirty = false;
            _settings.Remember(workingPath, _documentName);
            CloseSheet();
            RefreshUi();
            SetStatus($"Opened {_documentName}.");
        }
        catch (Exception exception)
        {
            SetStatus($"Open failed: {exception.Message}");
        }
    }

    private async Task OpenRecentAsync(RecentSketch recent)
    {
        if (!await ConfirmDiscardIfDirtyAsync())
            return;

        CloseSheet();
        if (!File.Exists(recent.Path))
        {
            SetStatus("That recent sketch is no longer available.");
            return;
        }

        await LoadWorkingCopyAsync(recent.Path, recent.Name);
    }

    private async Task<bool> SaveAsync()
    {
        var json = SerializeCurrentDocument();
        if (_sourceFile is not null)
        {
            try
            {
                await WriteStorageTextAsync(_sourceFile, json);
            }
            catch (Exception exception)
            {
                SetStatus($"Source save failed: {exception.Message}");
                return false;
            }
        }

        await SaveWorkingCopyAsync(json);
        _isDirty = false;
        RefreshUi();
        SetStatus($"Saved {_documentName}.");
        return true;
    }

    private async Task<bool> SaveAsAsync()
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is not { CanSave: true })
        {
            SetStatus("This host cannot save files.");
            return false;
        }

        var result = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Sketch As",
            SuggestedFileName = _documentName.EndsWith(".sketchjson", StringComparison.OrdinalIgnoreCase)
                ? _documentName
                : $"{_documentName}.sketchjson",
            DefaultExtension = "sketchjson",
            FileTypeChoices = [SketchJsonType],
        });
        if (result is null)
            return false;

        try
        {
            var json = SerializeCurrentDocument();
            await WriteStorageTextAsync(result, json);
            _sourceFile = result;
            _documentName = string.IsNullOrWhiteSpace(result.Name) ? "Sketch.sketchjson" : result.Name;
            await SaveWorkingCopyAsync(json);
            _isDirty = false;
            CloseSheet();
            RefreshUi();
            SetStatus($"Saved {_documentName}.");
            return true;
        }
        catch (Exception exception)
        {
            SetStatus($"Save As failed: {exception.Message}");
            return false;
        }
    }

    private async Task SaveWorkingCopyAsync(string? json = null)
    {
        if (string.IsNullOrWhiteSpace(_workingPath))
            _workingPath = _settings.CreateWorkingPath(_documentName);

        json ??= SerializeCurrentDocument();
        await File.WriteAllTextAsync(_workingPath, json);
        _settings.Remember(_workingPath, _documentName);
    }

    private async void ScheduleWorkingSave()
    {
        _saveDebounce?.Cancel();
        _saveDebounce?.Dispose();
        var cancellation = new CancellationTokenSource();
        _saveDebounce = cancellation;

        try
        {
            await Task.Delay(300, cancellation.Token);
            await SaveWorkingCopyAsync();
        }
        catch (OperationCanceledException)
        {
            // A newer edit superseded this working-copy write.
        }
        catch (Exception exception)
        {
            SetStatus($"Recovery copy failed: {exception.Message}");
        }
    }

    private async Task<bool> ConfirmDiscardIfDirtyAsync()
    {
        if (!_isDirty)
            return true;

        var choice = await ShowChoiceSheetAsync(
            "Unsaved changes",
            "Save changes before continuing?",
            ("Save", "save"),
            ("Don't Save", "discard"),
            ("Cancel", "cancel"));
        return choice switch
        {
            "save" => await SaveAsync(),
            "discard" => true,
            _ => false,
        };
    }

    private void ApplyDocument(SketchDocument document)
    {
        _suppressDirty = true;
        try
        {
            _sketch.Document = document;
            _sketch.GridSize = document.Grid.Size;
            _sketch.GridVisible = document.Grid.Visible;
            _sketch.SnapEnabled = document.Grid.SnapEnabled;
        }
        finally
        {
            _suppressDirty = false;
        }

        RefreshUi();
    }

    private string SerializeCurrentDocument()
    {
        var document = _sketch.Document ?? NewDocument(_sketch.GridSize, _sketch.GridVisible, _sketch.SnapEnabled);
        document.Grid.Size = _sketch.GridSize;
        document.Grid.Visible = _sketch.GridVisible;
        document.Grid.SnapEnabled = _sketch.SnapEnabled;
        return SketchJson.Serialize(document);
    }

    private static SketchDocument NewDocument(
        double gridSize = 20,
        bool gridVisible = true,
        bool snapEnabled = true) =>
        new()
        {
            Grid =
            {
                Size = gridSize,
                Visible = gridVisible,
                SnapEnabled = snapEnabled,
            },
        };

    private void ShowDocumentSheet()
    {
        var body = new StackPanel { Spacing = 8, Margin = new Thickness(16, 0, 16, 18) };
        body.Children.Add(SheetNavigationButton("New sketch", () => _ = NewAsync()));
        body.Children.Add(SheetNavigationButton("Open sketch", () => _ = OpenAsync()));
        body.Children.Add(SheetNavigationButton("Recent sketches", ShowRecentSheet));
        body.Children.Add(SheetNavigationButton("Save", () => _ = SaveAsync()));
        body.Children.Add(SheetNavigationButton("Save As", () => _ = SaveAsAsync()));
        ShowSheet("Document", body);
    }

    private void ShowRecentSheet()
    {
        var body = new StackPanel { Spacing = 8, Margin = new Thickness(16, 0, 16, 18) };
        body.Children.Add(SheetButton("Back", ShowDocumentSheet));

        if (_settings.Recent.Count == 0)
        {
            body.Children.Add(new TextBlock
            {
                Text = "No saved working copies yet.",
                Foreground = GraphicalProfile.MutedBrush,
                Margin = new Thickness(0, 10),
            });
        }
        else
        {
            foreach (var recent in _settings.Recent)
            {
                var label = File.Exists(recent.Path)
                    ? recent.Name
                    : $"{recent.Name} (unavailable)";
                var button = SheetButton(label, () => _ = OpenRecentAsync(recent));
                button.IsEnabled = File.Exists(recent.Path);
                body.Children.Add(button);
            }
        }

        ShowSheet("Recent sketches", body);
    }

    private void ShowMoreSheet()
    {
        var body = new StackPanel { Spacing = 8, Margin = new Thickness(16, 0, 16, 18) };
        body.Children.Add(SheetNavigationButton("All tools", ShowToolSheet));
        body.Children.Add(SheetNavigationButton("Properties", ShowPropertiesSheet));
        body.Children.Add(SheetNavigationButton("Layers", ShowLayersSheet));
        body.Children.Add(SheetNavigationButton("Edit", ShowEditSheet));
        body.Children.Add(SheetNavigationButton("Export", ShowExportSheet));
        body.Children.Add(SheetNavigationButton("Keyboard help", ShowShortcutsSheet));
        ShowSheet("More", body);
    }

    private void ShowToolSheet()
    {
        var body = new StackPanel { Spacing = 10, Margin = new Thickness(16, 0, 16, 18) };
        AddSheetHeading(body, "Draw");
        AddToolButton(body, "Pen", SketchTool.Pen);
        AddToolButton(body, "Line", SketchTool.Line);
        AddToolButton(body, "Spline", SketchTool.Spline);

        AddSheetHeading(body, "Shapes");
        AddToolButton(body, "Box", SketchTool.Rect);
        AddToolButton(body, "Circle", SketchTool.Ellipse);
        AddToolButton(body, "Speech bubble", SketchTool.SpeechBubble);

        AddSheetHeading(body, "Text");
        AddToolButton(body, "Text label", SketchTool.Text);
        AddToolButton(body, "Text box", SketchTool.TextBox);

        AddSheetHeading(body, "Edit");
        AddToolButton(body, "Eraser", SketchTool.Eraser);
        AddToolButton(body, "Paint bucket", SketchTool.Fill);
        AddToolButton(body, "Select", SketchTool.Select);
        ShowSheet("All tools", body);
    }

    private void ShowPropertiesSheet()
    {
        var body = new StackPanel { Spacing = 12, Margin = new Thickness(16, 0, 16, 18) };
        body.Children.Add(new TextBlock
        {
            Text = $"New {_sketch.Tool} elements",
            Foreground = GraphicalProfile.MutedBrush,
            FontSize = 12,
        });

        body.Children.Add(ToggleRow("Snap to grid", _sketch.SnapEnabled, value =>
        {
            _sketch.SnapEnabled = value;
            TouchSettingChanged();
        }));
        body.Children.Add(ToggleRow("Meetup", _sketch.MeetupEnabled, value =>
        {
            _sketch.MeetupEnabled = value;
            TouchSettingChanged();
        }));
        body.Children.Add(ToggleRow("Grid", _sketch.GridVisible, value =>
        {
            _sketch.GridVisible = value;
            TouchSettingChanged();
        }));
        body.Children.Add(ToggleRow("Fill closed shapes", _sketch.FillEnabled, value =>
        {
            _sketch.FillEnabled = value;
            TouchSettingChanged();
        }));

        body.Children.Add(SliderRow(
            "Grid size",
            5,
            80,
            _sketch.GridSize,
            value =>
            {
                _sketch.GridSize = value;
                TouchSettingChanged();
            }));
        body.Children.Add(SliderRow(
            "Stroke width",
            0.25,
            16,
            _sketch.StrokeWidth,
            value =>
            {
                _sketch.StrokeWidth = value;
                TouchSettingChanged();
            }));

        AddSheetHeading(body, "Color");
        var colors = new WrapPanel
        {
            Orientation = AvaloniaOrientation.Horizontal,
            ItemWidth = 48,
            ItemHeight = 48,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        foreach (var hex in Palette)
        {
            var swatch = hex;
            var button = new AvaloniaButton
            {
                Content = string.Empty,
                Width = 42,
                Height = 42,
                Background = BrushFromHex(swatch),
                BorderBrush = GraphicalProfile.BorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(0),
            };
            AutomationProperties.SetName(button, $"Color {swatch}");
            button.Click += (_, _) => SetStrokeColor(swatch);
            colors.Children.Add(button);
        }
        body.Children.Add(colors);
        body.Children.Add(SheetButton("Custom color…", ShowCustomColorSheet));

        AddSheetHeading(body, "Stroke style");
        var styles = new WrapPanel
        {
            Orientation = AvaloniaOrientation.Horizontal,
            ItemWidth = 78,
            ItemHeight = 48,
        };
        foreach (var style in Enum.GetValues<SketchStrokeStyle>())
        {
            var local = style;
            styles.Children.Add(SheetButton(
                local.ToString(),
                () =>
                {
                    _sketch.StrokeStyle = local;
                    TouchSettingChanged();
                    ShowPropertiesSheet();
                },
                selected: _sketch.StrokeStyle == local));
        }
        body.Children.Add(styles);
        ShowSheet("Properties", body);
    }

    private void ShowCustomColorSheet()
    {
        var input = new TextBox
        {
            Text = string.IsNullOrWhiteSpace(_sketch.StrokeColor) ? "#1e1e1e" : _sketch.StrokeColor,
            PlaceholderText = "#RRGGBB or #AARRGGBB",
            MinHeight = 48,
            FontFamily = new FontFamily("monospace"),
        };
        var body = new StackPanel { Spacing = 12, Margin = new Thickness(16, 0, 16, 18) };
        body.Children.Add(new TextBlock
        {
            Text = "Use #RRGGBB or #AARRGGBB. Alpha is preserved in the sketch file.",
            Foreground = GraphicalProfile.MutedBrush,
            TextWrapping = TextWrapping.Wrap,
        });
        body.Children.Add(input);
        body.Children.Add(SheetButton("Apply", () =>
        {
            if (!TryParseColor(input.Text, out var color))
            {
                SetStatus("Enter a valid color.");
                return;
            }

            SetStrokeColor(color);
            CloseSheet();
        }, selected: true));
        ShowSheet("Custom color", body);
    }

    private void ShowLayersSheet()
    {
        var document = _sketch.Document;
        if (document is null)
            return;

        document.EnsureDefaultLayer();
        var body = new StackPanel { Spacing = 8, Margin = new Thickness(16, 0, 16, 18) };
        body.Children.Add(SheetButton("Add layer", () =>
        {
            var layer = document.AddLayer();
            TouchSettingChanged();
            SetStatus($"Added {layer.Name}.");
            ShowLayersSheet();
        }, selected: true));

        foreach (var layer in document.Layers)
        {
            var local = layer;
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
                ColumnSpacing = 6,
            };
            var select = SheetButton(
                document.ActiveLayerId == local.Id ? $"Active · {local.Name}" : local.Name,
                () =>
                {
                    document.ActiveLayerId = local.Id;
                    document.NotifyChanged();
                    TouchSettingChanged();
                    ShowLayersSheet();
                },
                selected: document.ActiveLayerId == local.Id);
            select.HorizontalContentAlignment = HorizontalAlignment.Left;
            var visible = SheetButton(local.Visible ? "Shown" : "Hidden", () =>
            {
                document.SetLayerVisible(local.Id, !local.Visible);
                TouchSettingChanged();
                ShowLayersSheet();
            });
            var locked = SheetButton(local.Locked ? "Locked" : "Open", () =>
            {
                document.SetLayerLocked(local.Id, !local.Locked);
                TouchSettingChanged();
                ShowLayersSheet();
            });
            row.Children.Add(select);
            Grid.SetColumn(visible, 1);
            Grid.SetColumn(locked, 2);
            row.Children.Add(visible);
            row.Children.Add(locked);
            body.Children.Add(row);
        }

        ShowSheet("Layers", body);
    }

    private void ShowEditSheet()
    {
        var body = new StackPanel { Spacing = 8, Margin = new Thickness(16, 0, 16, 18) };
        if (_sketch.HasInProgressDrawing)
        {
            body.Children.Add(SheetButton("Complete line / spline", () =>
            {
                _sketch.CompleteDrawing();
                SetStatus("Drawing completed.");
                ShowEditSheet();
            }, selected: true));
            body.Children.Add(SheetButton("Close shape", () =>
            {
                _sketch.CompleteDrawing(closeShape: true);
                SetStatus("Shape closed.");
                ShowEditSheet();
            }));
            body.Children.Add(SheetButton("Cancel drawing", () =>
            {
                _sketch.CancelPointerInteraction();
                SetStatus("Drawing cancelled.");
                ShowEditSheet();
            }));
        }

        body.Children.Add(SheetButton("Fuse selection", () =>
        {
            SetStatus(_sketch.FuseSelection() ? "Fused." : "Select at least two shapes.");
        }));
        body.Children.Add(SheetButton("Ungroup selection", () =>
        {
            SetStatus(_sketch.UngroupSelection() ? "Ungrouped." : "Nothing to ungroup.");
        }));
        body.Children.Add(SheetButton("Gridify selection", () =>
        {
            _sketch.GridifySelection();
            SetStatus("Gridified.");
        }));
        body.Children.Add(SheetButton("Undo", () =>
        {
            _sketch.Undo();
            SetStatus("Undo.");
        }));
        body.Children.Add(SheetButton("Redo", () =>
        {
            _sketch.Redo();
            SetStatus("Redo.");
        }));
        body.Children.Add(SheetButton("Delete selection", () =>
        {
            _sketch.Document?.DeleteSelection();
            SetStatus("Selection deleted.");
        }));
        body.Children.Add(SheetNavigationButton("Paste image", () => _ = PasteImageAsync()));
        body.Children.Add(SheetButton("Clear canvas", () =>
        {
            _sketch.Clear();
            SetStatus("Canvas cleared. Undo can restore it.");
        }));
        ShowSheet("Edit", body);
    }

    private void ShowExportSheet()
    {
        var body = new StackPanel { Spacing = 8, Margin = new Thickness(16, 0, 16, 18) };
        body.Children.Add(SheetNavigationButton("Copy PNG", () => _ = CopyPngAsync()));
        body.Children.Add(SheetNavigationButton("Save PNG file", () => _ = SavePngAsync()));
        body.Children.Add(SheetNavigationButton("Copy SVG", () => _ = CopySvgAsync()));
        body.Children.Add(SheetNavigationButton("Save SVG file", () => _ = SaveSvgAsync()));
        ShowSheet("Export", body);
    }

    private void ShowShortcutsSheet()
    {
        var body = new StackPanel { Spacing = 8, Margin = new Thickness(16, 0, 16, 18) };
        body.Children.Add(new TextBlock
        {
            Text = "Hardware keyboard commands remain available when Android has a physical keyboard.",
            Foreground = GraphicalProfile.MutedBrush,
            TextWrapping = TextWrapping.Wrap,
        });
        foreach (var (action, shortcut) in new[]
        {
            ("New / Open / Save", "Ctrl+N · Ctrl+O · Ctrl+S"),
            ("Undo / Redo", "Ctrl+Z · Ctrl+Y"),
            ("Select / Pen / Line", "Ctrl+M · Ctrl+P · Ctrl+L"),
            ("Spline / Box / Circle", "Ctrl+U · Ctrl+R · Ctrl+C"),
            ("Text / Text box", "Ctrl+T · Ctrl+X"),
            ("Eraser / Fill", "Ctrl+E · Ctrl+K"),
            ("Complete / close", "Enter · Ctrl+Enter"),
            ("Pan", "Space + drag"),
        })
        {
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            };
            row.Children.Add(new TextBlock { Text = action, Foreground = GraphicalProfile.TextBrush });
            var shortcutText = new TextBlock
            {
                Text = shortcut,
                Foreground = GraphicalProfile.MutedBrush,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            Grid.SetColumn(shortcutText, 1);
            row.Children.Add(shortcutText);
            body.Children.Add(row);
        }
        ShowSheet("Keyboard help", body);
    }

    private void SelectTool(SketchTool tool)
    {
        _sketch.Tool = tool;
        SetStatus($"{tool} selected.");
        RefreshUi();
    }

    private void OnDocumentChanged()
    {
        if (!_suppressDirty)
        {
            _isDirty = true;
            ScheduleWorkingSave();
        }

        RefreshUi();
    }

    private void TouchSettingChanged()
    {
        _isDirty = true;
        ScheduleWorkingSave();
        RefreshUi();
    }

    private void RefreshUi()
    {
        var document = _sketch.Document;
        var count = document?.Elements.Count ?? 0;
        var selection = document?.Selection.Count ?? 0;
        _title.Text = _isDirty
            ? $"Sketch Studio · {_documentName}*"
            : $"Sketch Studio · {_documentName}";
        _saveButton.Content = _isDirty ? "Save*" : "Save";
        _status.Text =
            $"{_sketch.Tool} · {count} element{(count == 1 ? string.Empty : "s")} · sel {selection} · {_sketch.StrokeColor} · w{_sketch.StrokeWidth:0.##}"
            + (_sketch.FillEnabled ? " · fill" : string.Empty);
        RebuildBottomBar();
    }

    private void SetStatus(string text)
    {
        _status.Text = text;
    }

    private void ShowSheet(string title, Control body)
    {
        _sheetTitle.Text = title;
        _sheetContent.Content = body;
        _sheetLayer.IsVisible = true;
    }

    private void CloseSheet()
    {
        var choice = _choiceCompletion;
        _choiceCompletion = null;
        _sheetLayer.IsVisible = false;
        _sheetContent.Content = null;
        choice?.TrySetResult(null);
    }

    private async Task<string?> ShowChoiceSheetAsync(
        string title,
        string message,
        params (string Label, string Value)[] choices)
    {
        _choiceCompletion?.TrySetResult(null);
        var completion = new TaskCompletionSource<string?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _choiceCompletion = completion;

        var body = new StackPanel { Spacing = 10, Margin = new Thickness(16, 0, 16, 18) };
        body.Children.Add(new TextBlock
        {
            Text = message,
            Foreground = GraphicalProfile.MutedBrush,
            TextWrapping = TextWrapping.Wrap,
        });
        foreach (var choice in choices)
        {
            var local = choice;
            body.Children.Add(SheetButton(local.Label, () => ResolveChoice(local.Value)));
        }

        ShowSheet(title, body);
        return await completion.Task;
    }

    private void ResolveChoice(string value)
    {
        var completion = _choiceCompletion;
        _choiceCompletion = null;
        _sheetLayer.IsVisible = false;
        _sheetContent.Content = null;
        completion?.TrySetResult(value);
    }

    private AvaloniaButton SheetNavigationButton(string label, Action action) =>
        SheetButton(label, () =>
        {
            CloseSheet();
            action();
        });

    private AvaloniaButton SheetButton(string label, Action action, bool selected = false)
    {
        var button = CreateButton(label, action, selected);
        button.HorizontalContentAlignment = HorizontalAlignment.Left;
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        return button;
    }

    private void AddToolButton(StackPanel body, string label, SketchTool tool) =>
        body.Children.Add(SheetNavigationButton(
            _sketch.Tool == tool ? $"{label} · active" : label,
            () => SelectTool(tool)));

    private static void AddSheetHeading(StackPanel body, string text) =>
        body.Children.Add(new TextBlock
        {
            Text = text.ToUpperInvariant(),
            Foreground = GraphicalProfile.AccentBrush,
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 10, 0, 0),
        });

    private static Control ToggleRow(string label, bool value, Action<bool> changed)
    {
        var check = new AvaloniaCheckBox
        {
            Content = label,
            IsChecked = value,
            MinHeight = 48,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        check.IsCheckedChanged += (_, _) => changed(check.IsChecked == true);
        return check;
    }

    private static Control SliderRow(
        string label,
        double minimum,
        double maximum,
        double value,
        Action<double> changed)
    {
        var slider = new Slider
        {
            Minimum = minimum,
            Maximum = maximum,
            Value = value,
            MinHeight = 48,
            VerticalAlignment = VerticalAlignment.Center,
        };
        slider.PropertyChanged += (_, args) =>
        {
            if (args.Property == RangeBase.ValueProperty)
                changed(slider.Value);
        };
        return new StackPanel
        {
            Spacing = 3,
            Children =
            {
                new TextBlock
                {
                    Text = label,
                    Foreground = GraphicalProfile.MutedBrush,
                    FontSize = 12,
                },
                slider,
            },
        };
    }

    private AvaloniaButton CreateButton(string label, Action action, bool selected = false)
    {
        var button = new AvaloniaButton
        {
            Content = label,
            MinWidth = 48,
            MinHeight = 48,
            Padding = new Thickness(10, 7),
            Background = selected
                ? GraphicalProfile.AccentFillBrush
                : GraphicalProfile.RaisedBrush,
            Foreground = selected
                ? GraphicalProfile.OnAccentFillBrush
                : GraphicalProfile.TextBrush,
            BorderBrush = GraphicalProfile.BorderBrush,
            BorderThickness = new Thickness(1),
        };
        AutomationProperties.SetName(button, label);
        button.Click += (_, _) => action();
        return button;
    }

    private void SetStrokeColor(string hex)
    {
        _sketch.StrokeColor = hex;
        _sketch.FillColor = hex;
        TouchSettingChanged();
        SetStatus($"Color {hex}.");
    }

    private static bool TryParseColor(string? text, out string color)
    {
        color = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        try
        {
            var parsed = Color.Parse(text.Trim());
            color = parsed.A == 255
                ? $"#{parsed.R:X2}{parsed.G:X2}{parsed.B:X2}"
                : $"#{parsed.A:X2}{parsed.R:X2}{parsed.G:X2}{parsed.B:X2}";
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static IBrush BrushFromHex(string hex)
    {
        try
        {
            return new SolidColorBrush(Color.Parse(hex));
        }
        catch
        {
            return GraphicalProfile.TextBrush;
        }
    }

    private static bool IsShapeTool(SketchTool tool) =>
        tool is SketchTool.Rect
            or SketchTool.Ellipse
            or SketchTool.SpeechBubble
            or SketchTool.TextBox;

    private async Task PasteImageAsync()
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
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
                using var stream = new MemoryStream();
                bitmap.Save(stream, PngBitmapEncoderOptions.Default);
                var placed = _sketch.PasteImage(stream.ToArray(), _sketch.ViewportCenterWorld());
                SetStatus(placed is null ? "Could not decode clipboard image." : "Pasted image.");
            }
        }
        catch (Exception exception)
        {
            SetStatus($"Paste failed: {exception.Message}");
        }
    }

    private async Task CopyPngAsync()
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        var document = _sketch.Document;
        if (clipboard is null || document is null)
        {
            SetStatus("Clipboard is unavailable.");
            return;
        }

        try
        {
            var png = SketchExport.ToPng(document, opaqueBackground: true);
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
        catch (Exception exception)
        {
            SetStatus($"PNG copy failed: {exception.Message}");
        }
    }

    private async Task CopySvgAsync()
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        var document = _sketch.Document;
        if (clipboard is null || document is null)
        {
            SetStatus("Clipboard is unavailable.");
            return;
        }

        try
        {
            var svg = SketchExport.ToSvg(document);
            await clipboard.SetTextAsync(svg);
            SetStatus($"Copied SVG ({svg.Length:N0} chars).");
        }
        catch (Exception exception)
        {
            SetStatus($"SVG copy failed: {exception.Message}");
        }
    }

    private Task SavePngAsync()
    {
        var document = _sketch.Document;
        return document is null
            ? Task.CompletedTask
            : SaveBytesAsync(
                "Save PNG As",
                "untitled.png",
                PngType,
                SketchExport.ToPng(document, opaqueBackground: true),
                "PNG");
    }

    private Task SaveSvgAsync()
    {
        var document = _sketch.Document;
        return document is null
            ? Task.CompletedTask
            : SaveBytesAsync(
                "Save SVG As",
                "untitled.svg",
                SvgType,
                Encoding.UTF8.GetBytes(SketchExport.ToSvg(document)),
                "SVG");
    }

    private async Task SaveBytesAsync(
        string title,
        string suggestedName,
        FilePickerFileType type,
        byte[] bytes,
        string format)
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is not { CanSave: true })
        {
            SetStatus("This host cannot save files.");
            return;
        }

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = type == PngType ? "png" : "svg",
            FileTypeChoices = [type],
        });
        if (file is null)
            return;

        try
        {
            await WriteStorageBytesAsync(file, bytes);
            SetStatus($"Saved {format} {file.Name}.");
        }
        catch (Exception exception)
        {
            SetStatus($"{format} save failed: {exception.Message}");
        }
    }

    private static async Task WriteStorageTextAsync(IStorageFile file, string text)
    {
        await WriteStorageBytesAsync(file, Encoding.UTF8.GetBytes(text));
    }

    private static async Task WriteStorageBytesAsync(IStorageFile file, byte[] bytes)
    {
        await using var stream = await file.OpenWriteAsync();
        if (stream.CanSeek)
        {
            stream.Position = 0;
            if (stream.CanWrite)
                stream.SetLength(0);
        }

        await stream.WriteAsync(bytes);
        await stream.FlushAsync();
    }

    internal void BeginMultiTouch() => _sketch.CancelPointerInteraction();

    internal void ApplyMultiTouchZoom(double factor, Point focus) => _sketch.ZoomAt(focus, factor);

    internal void PanMultiTouch(Vector delta) => _sketch.PanBy(delta);

    private IClipboard? GetClipboard() => TopLevel.GetTopLevel(this)?.Clipboard;
}
