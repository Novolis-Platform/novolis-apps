using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DraftStudio.Services;
using DraftStudio.Ui;
using Novolis.Avalonia.Agent;
using Novolis.Avalonia.Agent.Protocol;
using Novolis.Avalonia.Cad.Commands;
using Novolis.Avalonia.Cad.Core;
using Novolis.Avalonia.Cad.Services;
using Novolis.Avalonia.Cad.Session;
using Novolis.Avalonia.Cad.Ui;
using Novolis.Avalonia.Raylib;
using Novolis.Avalonia.Studio;
using Novolis.Cad.Primitives;

namespace DraftStudio;

internal sealed class MainWindow : Window
{
    private readonly CadSessionService _cad;
    private readonly CadDocumentSession _session;
    private readonly CadEditorSettings _settings;
    private readonly CadCommandBus _bus;
    private readonly CadCommandDispatcher _dispatcher;
    private readonly CadToolController _tools;
    private readonly CadModelRenderer _modelRenderer;
    private readonly DraftArtifactDumper _artifacts;
    private CadEditorSurface _editor = null!;
    private bool _dumpBusy;

    private CadDraftViewport _draftViewport = null!;
    private RaylibHostControl _raylibHost = null!;
    private Panel _viewportStack = null!;
    private StudioCommandBar _commandBar = null!;
    private StudioFeedback _feedback = null!;
    private ListBox _entityList = null!;
    private TextBlock _inspector = null!;
    private ComboBox _unitCombo = null!;
    private CheckBox _snapCheck = null!;
    private ComboBox _gridCombo = null!;
    private CheckBox _continuousCheck = null!;
    private CheckBox _isolateCheck = null!;
    private NumericUpDown _elevationBox = null!;
    private StackPanel _toolStrip = null!;
    private CadViewMode _viewMode = CadViewMode.Draft;
    private bool _orbiting;
    private Point _lastPointer;
    private bool _suppressList;
    private bool _suppressUnit;

    public MainWindow(CadSessionService cad)
    {
        _cad = cad;
        _session = cad.Document;
        _settings = cad.Settings;
        _bus = cad.Bus;
        _dispatcher = cad.Dispatcher;
        _tools = new CadToolController(_dispatcher, _settings);
        _modelRenderer = new CadModelRenderer(_session);
        _artifacts = new DraftArtifactDumper(_session, _settings);
        _cad.ExportRoot = Path.Combine(_settings.DataRoot, "exports");
        _cad.FitHandler = OnFit;

        Title = "Draft Studio";
        Width = 1400;
        Height = 900;

        Content = BuildLayout();
        _cad.Editor = _editor;

        _dispatcher.FitRequested += OnFit;
        _dispatcher.SaveRequested += OnSave;
        _dispatcher.DumpArtifactsRequested += () => _ = OnDumpArtifactsAsync();
        _dispatcher.ToolChanged += () =>
        {
            UpdateCommandPrompt();
            HighlightActiveTool();
        };
        _dispatcher.ElevationChanged += SyncElevationUi;
        _tools.Changed += UpdateCommandPrompt;
        _session.Changed += RefreshUi;
        _bus.Changed += RefreshUi;
        _draftViewport.ViewChanged += UpdateStatus;

        Opened += OnOpened;
        Closing += (_, _) => _settings.Save();
        KeyDown += OnKeyDown;
    }

    private Control BuildLayout()
    {
        var chrome = StudioChrome.Create();
        _feedback = chrome.CreateFeedback();
        AgentProperties.SetId(chrome.StatusLine, "draft.status");
        AgentProperties.SetId(chrome.FlashLine, "draft.flash");

        // Top: file + edit + camera + view + units (no shape tools — those live above the command bar)
        var toolbar = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(8, 4),
        };
        AgentProperties.SetId(toolbar, "draft.toolbar");

        toolbar.Children.Add(Btn("New", () => _ = OnNewAsync(), "draft.tool.new", "Ctrl+N"));
        toolbar.Children.Add(Btn("Open…", () => _ = OnOpenAsync(), "draft.tool.open", "Ctrl+O"));
        toolbar.Children.Add(Btn("Save", OnSave, "draft.tool.save", "Ctrl+S"));
        toolbar.Children.Add(Btn("Save As…", () => _ = OnSaveAsAsync(), "draft.tool.saveAs", "Ctrl+Shift+S"));
        toolbar.Children.Add(Sep());
        toolbar.Children.Add(Btn("Undo", () => _cad.Execute(new CadCommandDto { ActionId = CadSessionActionIds.Undo }), "draft.undo", "Ctrl+Z"));
        toolbar.Children.Add(Btn("Redo", () => _cad.Execute(new CadCommandDto { ActionId = CadSessionActionIds.Redo }), "draft.redo", "Ctrl+Y"));
        toolbar.Children.Add(Btn("Delete", () => _cad.Execute(new CadCommandDto { ActionId = CadSessionActionIds.DeleteSelection }), "draft.tool.delete", "Del"));
        toolbar.Children.Add(Sep());
        toolbar.Children.Add(Btn("Fit", OnFit, "draft.fit", "F"));
        toolbar.Children.Add(Btn("Zoom +", () => _draftViewport.ZoomBy(1.25), "draft.camera.zoomIn", "Ctrl+="));
        toolbar.Children.Add(Btn("Zoom −", () => _draftViewport.ZoomBy(0.8), "draft.camera.zoomOut", "Ctrl+-"));
        toolbar.Children.Add(Btn("Pan ↺", () => _draftViewport.ResetView(), "draft.camera.reset", "Home"));
        toolbar.Children.Add(Sep());
        toolbar.Children.Add(Btn("Draft", () => SetViewMode(CadViewMode.Draft), "draft.view.draft"));
        toolbar.Children.Add(Btn("Model", () => SetViewMode(CadViewMode.Model), "draft.view.model"));
        toolbar.Children.Add(Sep());
        toolbar.Children.Add(Btn("Export Phys…", () => _ = OnExportPhysAsync(), "draft.export.phys"));
        toolbar.Children.Add(Btn("Dump…", () => _ = OnDumpArtifactsAsync(), "draft.dump", "Save + draft/model/window PNGs"));
        toolbar.Children.Add(Sep());

        toolbar.Children.Add(Label("Units"));
        _unitCombo = new ComboBox
        {
            Width = 110,
            VerticalAlignment = VerticalAlignment.Center,
            ItemsSource = new[]
            {
                new UnitChoice(CadUnits.Meter, "Meters (m)"),
                new UnitChoice(CadUnits.Centimeter, "Centimeters (cm)"),
                new UnitChoice(CadUnits.Millimeter, "Millimeters (mm)"),
                new UnitChoice(CadUnits.Inch, "Inches (in)"),
            },
        };
        AgentProperties.SetId(_unitCombo, "draft.units", AgentRoleNames.ComboBox);
        _unitCombo.SelectionChanged += OnUnitChanged;
        toolbar.Children.Add(_unitCombo);

        _snapCheck = new CheckBox
        {
            Content = "Snap",
            IsChecked = true,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        };
        AgentProperties.SetId(_snapCheck, "draft.snap", AgentRoleNames.CheckBox);
        _snapCheck.IsCheckedChanged += (_, _) =>
        {
            _settings.Settings.SnapToGrid = _snapCheck.IsChecked == true;
            _draftViewport.InvalidateVisual();
        };
        toolbar.Children.Add(_snapCheck);

        toolbar.Children.Add(Label("Grid"));
        _gridCombo = new ComboBox
        {
            Width = 100,
            VerticalAlignment = VerticalAlignment.Center,
            ItemsSource = new[]
            {
                new GridChoice(0.1f, "0.1 m"),
                new GridChoice(0.25f, "0.25 m"),
                new GridChoice(0.5f, "0.5 m"),
                new GridChoice(1f, "1 m"),
                new GridChoice(2f, "2 m"),
            },
        };
        AgentProperties.SetId(_gridCombo, "draft.grid", AgentRoleNames.ComboBox);
        _gridCombo.SelectionChanged += OnGridChanged;
        toolbar.Children.Add(_gridCombo);

        _editor = new CadEditorSurface(_session, _settings, _bus, _dispatcher, _tools, _modelRenderer);
        _draftViewport = _editor.DraftViewport;
        AgentProperties.SetId(_draftViewport, "draft.viewport");
        _raylibHost = _editor.ModelHost;
        _raylibHost.HorizontalAlignment = HorizontalAlignment.Stretch;
        _raylibHost.VerticalAlignment = VerticalAlignment.Stretch;
        AgentProperties.SetId(_raylibHost, "draft.viewport.model");
        _raylibHost.PointerPressed += OnModelPointerPressed;
        _raylibHost.PointerMoved += OnModelPointerMoved;
        _raylibHost.PointerReleased += OnModelPointerReleased;
        _raylibHost.PointerWheelChanged += OnModelWheel;

        _viewportStack = _editor;

        // Shape / mode strip immediately above the command bar
        _toolStrip = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(8, 4, 8, 2),
        };
        AgentProperties.SetId(_toolStrip, "draft.shapes");
        _toolStrip.Children.Add(ToolBtn("Select", () => ExecTool("select"), "draft.tool.select"));
        _toolStrip.Children.Add(ToolBtn("Line", () => ExecTool("line"), "draft.tool.line", "L"));
        _toolStrip.Children.Add(ToolBtn("Circle", () => ExecTool("circle"), "draft.tool.circle", "C"));
        _toolStrip.Children.Add(ToolBtn("Rect", () => ExecTool("rect"), "draft.tool.rect", "R"));
        _toolStrip.Children.Add(ToolBtn("Spline", () => ExecTool("spline"), "draft.tool.spline", "P"));
        _toolStrip.Children.Add(ToolBtn("Box", () => ExecCommand("Box(1,1,1)"), "draft.tool.box"));
        _toolStrip.Children.Add(Sep());

        _continuousCheck = new CheckBox
        {
            Content = "Continuous",
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTip.SetTip(_continuousCheck, "Line: chain from last endpoint (Esc ends)");
        AgentProperties.SetId(_continuousCheck, "draft.continuous", AgentRoleNames.CheckBox);
        _continuousCheck.IsCheckedChanged += (_, _) =>
        {
            _tools.ContinuousLine = _continuousCheck.IsChecked == true;
            UpdateCommandPrompt();
        };
        _toolStrip.Children.Add(_continuousCheck);

        _toolStrip.Children.Add(Sep());
        _toolStrip.Children.Add(Label("Level"));
        _elevationBox = new NumericUpDown
        {
            Width = 100,
            Minimum = -1000,
            Maximum = 1000,
            Increment = 0.5m,
            FormatString = "0.##",
            VerticalAlignment = VerticalAlignment.Center,
        };
        AgentProperties.SetId(_elevationBox, "draft.elevation", AgentRoleNames.TextBox);
        ToolTip.SetTip(_elevationBox, "Drawing plane elevation (world Y). Plan view is XZ.");
        _elevationBox.ValueChanged += OnElevationChanged;
        _toolStrip.Children.Add(_elevationBox);
        _toolStrip.Children.Add(Btn("+1", () => NudgeElevation(1f), "draft.elevation.up"));
        _toolStrip.Children.Add(Btn("−1", () => NudgeElevation(-1f), "draft.elevation.down"));
        _toolStrip.Children.Add(Btn("0", () => SetElevation(0f), "draft.elevation.zero"));

        _isolateCheck = new CheckBox
        {
            Content = "Isolate",
            IsChecked = true,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        };
        ToolTip.SetTip(_isolateCheck, "Dim / skip hit-test for sketch entities off the current level");
        AgentProperties.SetId(_isolateCheck, "draft.isolate", AgentRoleNames.CheckBox);
        _isolateCheck.IsCheckedChanged += (_, _) =>
        {
            _settings.Settings.IsolateLevel = _isolateCheck.IsChecked == true;
            _draftViewport.InvalidateVisual();
        };
        _toolStrip.Children.Add(_isolateCheck);

        _commandBar = new StudioCommandBar();
        AgentProperties.SetId(_commandBar, "draft.commandBar.host");
        if (_commandBar.Content is Border { Child: Panel commandRow })
        {
            foreach (var child in commandRow.Children)
            {
                if (child is TextBox input)
                    AgentProperties.SetId(input, "draft.commandBar", AgentRoleNames.TextBox);
            }
        }
        _commandBar.Submitted += (_, e) =>
        {
            var result = _cad.Execute(new CadCommandDto
            {
                ActionId = CadSessionActionIds.RunCommand,
                Prompt = e.Text,
            });
            if (!result.Ok)
                _feedback.FlashError(result.Message);
            else
            {
                _feedback.SetStatus($"OK — {e.Text}");
                SyncElevationUi();
            }

            UpdateCommandPrompt();
        };
        _commandBar.Cancelled += (_, _) =>
        {
            _tools.Cancel();
            UpdateCommandPrompt();
        };

        var bottomStack = new StackPanel { Spacing = 0 };
        bottomStack.Children.Add(_toolStrip);
        bottomStack.Children.Add(_commandBar);

        var center = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        center.Children.Add(toolbar);
        Grid.SetRow(_viewportStack, 1);
        center.Children.Add(_viewportStack);
        Grid.SetRow(bottomStack, 2);
        center.Children.Add(bottomStack);

        var viewportWithStatus = StudioWorkspace.CreateViewportStack(center, chrome.FlashLine, chrome.StatusLine);

        _entityList = new ListBox();
        AgentProperties.SetId(_entityList, "draft.entities", AgentRoleNames.ListBox);
        _entityList.ItemTemplate = new FuncDataTemplate<CadEntity>((item, _) =>
            new TextBlock { Text = item.Summary, Margin = new Thickness(4) }, true);
        _entityList.SelectionChanged += (_, _) =>
        {
            if (_suppressList)
                return;
            if (_entityList.SelectedItem is CadEntity entity)
            {
                _session.SelectedId = entity.Id;
                _session.Notify();
            }
        };

        var left = new DockPanel { Margin = new Thickness(4) };
        var leftTitle = new TextBlock { Text = "Entities", FontWeight = FontWeight.SemiBold, Margin = new Thickness(4) };
        DockPanel.SetDock(leftTitle, Dock.Top);
        left.Children.Add(leftTitle);
        left.Children.Add(_entityList);

        _inspector = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(8),
            Opacity = 0.9,
        };
        AgentProperties.SetId(_inspector, "draft.inspector");
        var right = new DockPanel { Margin = new Thickness(4) };
        var rightTitle = new TextBlock { Text = "Inspector", FontWeight = FontWeight.SemiBold, Margin = new Thickness(4) };
        DockPanel.SetDock(rightTitle, Dock.Top);
        right.Children.Add(rightTitle);
        right.Children.Add(_inspector);

        return new DraftWorkspace(_settings, left, viewportWithStatus, right);
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        _cad.Editor = _editor;
        _session.OpenOrCreateDefault();
        _suppressUnit = true;
        SelectUnit(_settings.Settings.DisplayUnit);
        _snapCheck.IsChecked = _settings.Settings.SnapToGrid;
        SelectGrid(_settings.Settings.GridStep);
        _continuousCheck.IsChecked = _settings.Settings.ContinuousLine;
        _isolateCheck.IsChecked = _settings.Settings.IsolateLevel;
        SyncElevationUi();
        _suppressUnit = false;

        if (string.Equals(_settings.Settings.ViewMode, "model", StringComparison.OrdinalIgnoreCase))
            SetViewMode(CadViewMode.Model);
        else
            SetViewMode(CadViewMode.Draft);
        OnFit();
        UpdateCommandPrompt();
        HighlightActiveTool();
        UpdateStatus();
        _commandBar.FocusInput();
    }

    private void RefreshUi()
    {
        _suppressList = true;
        _entityList.ItemsSource = _session.Document.Entities.ToList();
        var selected = _session.SelectedEntity;
        _entityList.SelectedItem = selected;
        _suppressList = false;

        var unit = _settings.Settings.DisplayUnit;
        _inspector.Text = selected is null
            ? "No selection.\n\nShapes live above the command bar.\nLevel = drawing elevation (world Y).\nSelect a line/box/circle/rect for grips.\nContinuous Line chains segments.\nSpline: click near start to close.\n\nLevel(y) · Box(w,h,d) · …"
            : FormatInspector(selected, unit);

        var dirty = _session.IsDirty ? " *" : "";
        Title = $"Draft Studio — {Path.GetFileName(_session.DocumentPath)}{dirty}";
        _draftViewport.InvalidateVisual();
        if (_viewMode == CadViewMode.Model)
            _raylibHost.RequestFrame();
        UpdateStatus();
    }

    private static string FormatInspector(CadEntity selected, string unit)
    {
        var lines = new List<string>
        {
            selected.Summary,
            $"Kind: {selected.Kind}",
            $"Id: {selected.Id:N}",
            $"Display: {CadUnits.Abbreviation(unit)} (doc = m)",
        };
        if (selected.HalfExtents is { Length: >= 3 })
        {
            lines.Add(
                $"Size: {CadUnits.FormatLength(selected.HalfExtents[0] * 2, unit)} × " +
                $"{CadUnits.FormatLength(selected.HalfExtents[1] * 2, unit)} × " +
                $"{CadUnits.FormatLength(selected.HalfExtents[2] * 2, unit)}");
        }

        if (selected.Radius > 0)
            lines.Add($"Radius: {CadUnits.FormatLength(selected.Radius, unit)}");
        lines.Add($"Elevation: {CadUnits.FormatLength(CadVec.ElevationOf(selected), unit)}");
        return string.Join('\n', lines);
    }

    private void UpdateStatus()
    {
        var unit = _settings.Settings.DisplayUnit;
        var ppm = _draftViewport.PixelsPerMeter;
        var zoom = ppm > 0 ? CadUnits.FormatLength(100.0 / ppm, unit) + " / 100 px" : "—";
        var mode = _viewMode == CadViewMode.Model ? "Model" : "Draft (XZ)";
        var level = CadUnits.FormatLength(_settings.Settings.DrawElevation, unit);
        _feedback.SetStatus(
            $"{mode}  ·  {Path.GetFileName(_session.DocumentPath)}  ·  L={level}  ·  {CadUnits.Abbreviation(unit)}  ·  {zoom}");
    }

    private void UpdateCommandPrompt() =>
        _commandBar.PromptLabel = _tools.PromptHint;

    private void SetViewMode(CadViewMode mode)
    {
        _cad.Execute(new CadCommandDto
        {
            ActionId = CadSessionActionIds.SetViewMode,
            ViewMode = mode == CadViewMode.Model ? "model" : "draft",
        });
        _viewMode = _editor.ViewMode;
        if (mode == CadViewMode.Model)
        {
            _raylibHost.FrameWidth = System.Math.Max(64, (int)_viewportStack.Bounds.Width);
            _raylibHost.FrameHeight = System.Math.Max(64, (int)_viewportStack.Bounds.Height);
            _raylibHost.RequestFrame();
        }

        UpdateStatus();
    }

    private void OnFit()
    {
        _cad.Execute(new CadCommandDto { ActionId = CadSessionActionIds.Fit });
        if (_viewMode == CadViewMode.Model)
            _raylibHost.RequestFrame();
    }

    private void ExecTool(string tool)
    {
        var result = _cad.Execute(new CadCommandDto
        {
            ActionId = CadSessionActionIds.SetTool,
            Tool = tool,
        });
        if (!result.Ok)
            _feedback.FlashError(result.Message);
        UpdateCommandPrompt();
        HighlightActiveTool();
    }

    private void ExecCommand(string prompt)
    {
        var result = _cad.Execute(new CadCommandDto
        {
            ActionId = CadSessionActionIds.RunCommand,
            Prompt = prompt,
        });
        if (!result.Ok)
            _feedback.FlashError(result.Message);
        else
            UpdateCommandPrompt();
    }

    private void Run(string prompt) => ExecCommand(prompt);

    private async Task OnDumpArtifactsAsync()
    {
        if (_dumpBusy)
            return;
        _dumpBusy = true;
        try
        {
            var previous = _viewMode;
            var result = await _artifacts.DumpAllAsync(
                this,
                _draftViewport,
                _raylibHost,
                ensureModelViewAsync: async () =>
                {
                    SetViewMode(CadViewMode.Model);
                    await Task.Delay(80);
                },
                ensureDraftViewAsync: async () =>
                {
                    SetViewMode(CadViewMode.Draft);
                    await Task.Delay(40);
                });

            SetViewMode(previous);
            var bits = new List<string> { $"doc={Path.GetFileName(result.DocumentPath)}" };
            if (result.DraftPngPath is not null)
                bits.Add("draft.png");
            if (result.ModelPngPath is not null)
                bits.Add("model.png");
            if (result.WindowPngPath is not null)
                bits.Add("window.png");
            _feedback.Flash($"Dump → {_artifacts.DumpsDirectory} ({string.Join(", ", bits)})");
            UpdateStatus();
        }
        catch (Exception ex)
        {
            _feedback.FlashError($"Dump failed: {ex.Message}");
        }
        finally
        {
            _dumpBusy = false;
        }
    }

    private void OnSave()
    {
        var result = _cad.Execute(new CadCommandDto { ActionId = CadSessionActionIds.Save });
        if (result.Ok)
            _feedback.Flash(result.Message);
        else
            _feedback.FlashError(result.Message);
    }

    private async Task OnNewAsync()
    {
        if (!await ConfirmDiscardIfDirtyAsync())
            return;
        _session.NewDocument();
        OnFit();
        _feedback.Flash("New document");
    }

    private async Task OnOpenAsync()
    {
        if (!await ConfirmDiscardIfDirtyAsync())
            return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open CadJSON",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("CadJSON") { Patterns = ["*.cadjson", "*.json"] },
                FilePickerFileTypes.All,
            ],
        });
        if (files.Count == 0)
            return;

        var path = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            _feedback.FlashError("Could not open file");
            return;
        }

        try
        {
            _session.OpenFromPath(path);
            _settings.Save();
            OnFit();
            _feedback.Flash($"Opened {Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            _feedback.FlashError($"Open failed: {ex.Message}");
        }
    }

    private async Task OnSaveAsAsync()
    {
        var result = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save CadJSON As",
            SuggestedFileName = Path.GetFileName(_session.DocumentPath),
            DefaultExtension = "cadjson",
            FileTypeChoices =
            [
                new FilePickerFileType("CadJSON") { Patterns = ["*.cadjson"] },
            ],
        });
        var path = result?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
            return;

        if (!path.EndsWith(".cadjson", StringComparison.OrdinalIgnoreCase)
            && !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            path += ".cadjson";

        try
        {
            _session.SaveTo(path);
            _settings.Save();
            _feedback.Flash($"Saved {Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            _feedback.FlashError($"Save As failed: {ex.Message}");
        }
    }

    private async Task OnExportPhysAsync()
    {
        var suggested = Path.ChangeExtension(Path.GetFileName(_session.DocumentPath), ".cadphys.json")
                        ?? "draft.cadphys.json";
        var result = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Phys",
            SuggestedFileName = suggested,
            DefaultExtension = "cadphys.json",
            FileTypeChoices =
            [
                new FilePickerFileType("Cad Phys JSON") { Patterns = ["*.cadphys.json", "*.json"] },
            ],
        });
        var path = result?.TryGetLocalPath() ?? _settings.PhysDocumentPath;
        if (result is null && string.IsNullOrWhiteSpace(path))
            return;

        if (result is not null && string.IsNullOrWhiteSpace(path))
        {
            _feedback.FlashError("Could not resolve export path");
            return;
        }

        if (result is null)
        {
            // Fallback: write next to document when picker cancelled? Prefer abort.
            return;
        }

        try
        {
            var exportResult = _cad.Execute(new CadCommandDto
            {
                ActionId = CadSessionActionIds.ExportPhys,
                Path = path,
            });
            if (exportResult.Ok)
                _feedback.Flash(exportResult.Message);
            else
                _feedback.FlashError(exportResult.Message);
        }
        catch (Exception ex)
        {
            _feedback.FlashError($"Export failed: {ex.Message}");
        }
    }

    private async Task<bool> ConfirmDiscardIfDirtyAsync()
    {
        if (!_session.IsDirty)
            return true;

        var dlg = new Window
        {
            Title = "Unsaved changes",
            Width = 360,
            Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
        };
        var save = false;
        var discard = false;
        var cancel = true;
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(12),
        };
        var saveBtn = new Button { Content = "Save", Padding = new Thickness(12, 4) };
        var discardBtn = new Button { Content = "Don't Save", Padding = new Thickness(12, 4) };
        var cancelBtn = new Button { Content = "Cancel", Padding = new Thickness(12, 4) };
        saveBtn.Click += (_, _) => { save = true; cancel = false; dlg.Close(); };
        discardBtn.Click += (_, _) => { discard = true; cancel = false; dlg.Close(); };
        cancelBtn.Click += (_, _) => dlg.Close();
        buttons.Children.Add(saveBtn);
        buttons.Children.Add(discardBtn);
        buttons.Children.Add(cancelBtn);
        var body = new TextBlock
        {
            Text = "Save changes before continuing?",
            Margin = new Thickness(16),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var panel = new DockPanel();
        DockPanel.SetDock(buttons, Dock.Bottom);
        panel.Children.Add(buttons);
        panel.Children.Add(body);
        dlg.Content = panel;
        await dlg.ShowDialog(this);
        if (cancel)
            return false;
        if (save)
            OnSave();
        return discard || save;
    }

    private void OnElevationChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_suppressUnit || e.NewValue is null)
            return;
        _settings.Settings.DrawElevation = (float)e.NewValue.Value;
        _draftViewport.InvalidateVisual();
        UpdateStatus();
    }

    private void SyncElevationUi()
    {
        _suppressUnit = true;
        _elevationBox.Value = (decimal)_settings.Settings.DrawElevation;
        _suppressUnit = false;
        _draftViewport.InvalidateVisual();
        UpdateStatus();
    }

    private void SetElevation(float meters)
    {
        _settings.Settings.DrawElevation = meters;
        SyncElevationUi();
    }

    private void NudgeElevation(float deltaMeters) =>
        SetElevation(_settings.Settings.DrawElevation + deltaMeters);

    private void HighlightActiveTool()
    {
        var active = _dispatcher.ActiveTool;
        foreach (var child in _toolStrip.Children)
        {
            if (child is not Button btn || btn.Tag is not CadToolKind kind)
                continue;
            btn.FontWeight = kind == active ? FontWeight.Bold : FontWeight.Normal;
            btn.Opacity = kind == active ? 1.0 : 0.85;
        }
    }

    private void OnUnitChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressUnit || _unitCombo.SelectedItem is not UnitChoice choice)
            return;
        _settings.Settings.DisplayUnit = choice.Id;
        _draftViewport.InvalidateVisual();
        RefreshUi();
    }

    private void OnGridChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressUnit || _gridCombo.SelectedItem is not GridChoice choice)
            return;
        _settings.Settings.GridStep = choice.Meters;
        _draftViewport.InvalidateVisual();
    }

    private void SelectUnit(string unit)
    {
        if (_unitCombo.ItemsSource is IEnumerable<UnitChoice> items)
            _unitCombo.SelectedItem = items.FirstOrDefault(i => i.Id == unit) ?? items.First();
    }

    private void SelectGrid(float meters)
    {
        if (_gridCombo.ItemsSource is not IEnumerable<GridChoice> items)
            return;
        _gridCombo.SelectedItem = items.FirstOrDefault(i => System.Math.Abs(i.Meters - meters) < 1e-6)
                                  ?? items.FirstOrDefault(i => System.Math.Abs(i.Meters - 0.5f) < 1e-6)
                                  ?? items.First();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Home)
        {
            _draftViewport.ResetView();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.OemPlus || e.Key == Key.Add)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                _draftViewport.ZoomBy(1.25);
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.OemMinus || e.Key == Key.Subtract)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                _draftViewport.ZoomBy(0.8);
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.N && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _ = OnNewAsync();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.O && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _ = OnOpenAsync();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.S && e.KeyModifiers.HasFlag(KeyModifiers.Control)
            && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            _ = OnSaveAsAsync();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.S && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            OnSave();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Z && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _bus.Undo();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Y && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _bus.Redo();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete)
        {
            Run("Delete");
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F && !e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            OnFit();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && _tools.TryCommitSpline())
        {
            UpdateCommandPrompt();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            _tools.Cancel();
            UpdateCommandPrompt();
            e.Handled = true;
        }
    }

    private void OnModelPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(_raylibHost).Properties;
        if (props.IsLeftButtonPressed || props.IsMiddleButtonPressed)
        {
            _orbiting = true;
            _lastPointer = e.GetPosition(_raylibHost);
            e.Pointer.Capture(_raylibHost);
            e.Handled = true;
        }
    }

    private void OnModelPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_orbiting)
            return;
        var p = e.GetPosition(_raylibHost);
        var dx = (float)(p.X - _lastPointer.X);
        var dy = (float)(p.Y - _lastPointer.Y);
        _modelRenderer.OrbitDrag(dx, dy);
        _lastPointer = p;
        _raylibHost.RequestFrame();
        e.Handled = true;
    }

    private void OnModelPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_orbiting)
            return;
        _orbiting = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void OnModelWheel(object? sender, PointerWheelEventArgs e)
    {
        _modelRenderer.Zoom((float)e.Delta.Y);
        _raylibHost.RequestFrame();
        e.Handled = true;
    }

    private static Button Btn(string text, Action action, string agentId, string? tip = null)
    {
        var b = new Button { Content = text, Padding = new Thickness(10, 4), Margin = new Thickness(0, 2) };
        AgentProperties.SetId(b, agentId, AgentRoleNames.Button);
        if (tip is not null)
            ToolTip.SetTip(b, tip);
        b.Click += (_, _) => action();
        return b;
    }

    private Button ToolBtn(string text, Action action, string agentId, string? tip = null)
    {
        var kind = agentId switch
        {
            "draft.tool.select" => CadToolKind.Select,
            "draft.tool.line" => CadToolKind.Line,
            "draft.tool.circle" => CadToolKind.Circle,
            "draft.tool.rect" => CadToolKind.Rect,
            "draft.tool.spline" => CadToolKind.Spline,
            _ => (CadToolKind?)null,
        };
        var b = Btn(text, action, agentId, tip);
        if (kind is { } k)
            b.Tag = k;
        return b;
    }

    private static TextBlock Label(string text) => new()
    {
        Text = text,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(4, 0, 2, 0),
        Opacity = 0.85,
    };

    private static Control Sep() => new Border { Width = 10 };

    private sealed record UnitChoice(string Id, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record GridChoice(float Meters, string Label)
    {
        public override string ToString() => Label;
    }
}
