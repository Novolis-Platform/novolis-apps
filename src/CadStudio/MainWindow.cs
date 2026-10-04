using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Novolis.Agent.Core;
using Novolis.Avalonia.ThreeD;
using Novolis.Avalonia.ThreeD.Session;
using Novolis.Avalonia.ThreeD.Ui;
using Novolis.Avalonia.Agent;
using Novolis.Avalonia.Agent.Protocol;
using Novolis.Avalonia.Cad.Commands;
using Novolis.Avalonia.Cad.Core;
using Novolis.Avalonia.Cad.Services;
using Novolis.Avalonia.Cad.Session;
using Novolis.Avalonia.Cad.Ui;
using Novolis.Avalonia.GraphicalProfile;
using Novolis.Avalonia.Ship.Design;
using Novolis.Avalonia.Ship.Design.Services;
using Novolis.Avalonia.Ship.Design.Session;
using Novolis.Ship.Design;
using Novolis.Avalonia.Studio;
using Novolis.Cad.Primitives;
using Novolis.Cad.SceneBridge;
using Novolis.ThreeD;

namespace CadStudio;

internal sealed class MainWindow : Window
{
    private readonly CadSessionService _cad;
    private readonly CadSessionService _shipCad;
    private readonly SceneSessionService _scene;
    private readonly CadDocumentSession _doc;
    private readonly CadEditorSettings _settings;
    private readonly CadCommandBus _bus;
    private readonly CadCommandDispatcher _dispatcher;
    private readonly CadToolController _tools;
    private readonly CadToolController _shipTools;
    private readonly CadModelRenderer _modelRenderer;
    private readonly CadArtifactDumper _artifacts;
    private readonly ShipDesignSession _shipDesign;
    private readonly CadStudioDataMigration.Report _migration;

    private CadEditorSurface _cadEditor = null!;
    private CadEditorSurface _shipEditor = null!;
    private SceneEditorSurface _sceneEditor = null!;
    private Panel _host = null!;
    private Control _cadHost = null!;
    private Control _sceneHost = null!;
    private Control _shipHost = null!;
    private Control _draftBarHost = null!;
    private StudioFeedback _feedback = null!;
    private StudioCommandBar _commandBar = null!;
    private CheckBox _snapCheck = null!;
    private ComboBox _gridCombo = null!;
    private Button _lockNone = null!;
    private Button _lockX = null!;
    private Button _lockY = null!;
    private Button _lockZ = null!;
    private ComboBox _unitCombo = null!;
    private CheckBox _continuousCheck = null!;
    private CheckBox _isolateCheck = null!;
    private NumericUpDown _elevationBox = null!;
    private TextBlock _modeBanner = null!;
    private TextBlock _portsLine = null!;
    private Control _exportPhysBtn = null!;
    private Control _dumpBtn = null!;
    private Control _undoBtn = null!;
    private Control _redoBtn = null!;
    private Control _deleteBtn = null!;
    private Control _bridgeBtn = null!;
    private StudioWorkspace _workspace = StudioWorkspace.Draft2D;
    private bool _scenePresenting;
    private IDisposable? _shipAttachment;
    private bool _bridgeDirty = true;
    private bool _syncingDraftUi;
    private bool _dumpBusy;

    public MainWindow(
        CadSessionService cad,
        ShipCadSession shipCad,
        SceneSessionService scene,
        ShipDesignSession shipDesign,
        CadStudioDataMigration.Report migration)
    {
        _cad = cad;
        _shipCad = shipCad.Service;
        _scene = scene;
        _shipDesign = shipDesign;
        _migration = migration;
        _doc = cad.Document;
        _settings = cad.Settings;
        _bus = cad.Bus;
        _dispatcher = cad.Dispatcher;
        _tools = new CadToolController(_dispatcher, _settings);
        _shipTools = new CadToolController(shipCad.Dispatcher, shipCad.Settings);
        _modelRenderer = new CadModelRenderer(_doc, _settings);
        _artifacts = new CadArtifactDumper(_doc, _settings);
        _cad.ExportRoot = Path.Combine(_settings.DataRoot, "exports");
        _cad.FitHandler = () => _cad.Execute(new CadCommandDto { ActionId = CadSessionActionIds.Fit });
        _shipCad.FitHandler = () => _shipEditor?.Fit();

        Title = "Novolis CAD Studio";
        Width = 1480;
        Height = 920;
        MinWidth = 1100;
        MinHeight = 640;
        Background = GraphicalProfile.BackgroundBrush;

        Content = BuildLayout();
        _cad.Editor = _cadEditor;

        _cad.SceneBridged += OnSceneBridged;
        _cad.StudioWorkspaceRequested += id => SetStudioWorkspace(StudioWorkspaceIds.Parse(id));
        _doc.Changed += () =>
        {
            _bridgeDirty = true;
            RefreshTitle();
        };
        _bus.Changed += RefreshTitle;
        _scene.DocumentChanged += RefreshTitle;
        _dispatcher.ToolChanged += () => _commandBar.PromptLabel = _tools.PromptHint;

        Opened += OnOpened;
        Closing += (_, _) =>
        {
            if (_scenePresenting)
                _sceneEditor.StopPresenting();
            _shipAttachment?.Dispose();
            _settings.Save();
        };
        KeyDown += OnKeyDown;
    }

    private Control BuildLayout()
    {
        var chrome = StudioChrome.Create();
        _feedback = chrome.CreateFeedback();
        AgentProperties.SetId(chrome.StatusLine, "cad.studio.status");
        AgentProperties.SetId(chrome.FlashLine, "cad.studio.flash");

        var toolbar = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(10, 8, 10, 4),
        };
        AgentProperties.SetId(toolbar, "cad.studio.toolbar");

        toolbar.Children.Add(SectionLabel("File"));
        toolbar.Children.Add(Btn("New", () => _ = OnNewAsync(), "cad.studio.tool.new"));
        toolbar.Children.Add(Btn("Open…", () => _ = OnOpenAsync(), "cad.studio.tool.open"));
        toolbar.Children.Add(Btn("Save", OnSave, "cad.studio.tool.save"));
        toolbar.Children.Add(Btn("Save As…", () => _ = OnSaveAsAsync(), "cad.studio.tool.saveAs"));
        _exportPhysBtn = Btn("Export Phys…", () => _ = OnExportPhysAsync(), "cad.studio.tool.exportPhys");
        _dumpBtn = Btn("Dump…", () => _ = OnDumpArtifactsAsync(), "cad.studio.tool.dump");
        toolbar.Children.Add(_exportPhysBtn);
        toolbar.Children.Add(_dumpBtn);
        toolbar.Children.Add(Sep());
        toolbar.Children.Add(SectionLabel("Edit"));
        _undoBtn = Btn("Undo", OnUndo, "cad.studio.undo");
        _redoBtn = Btn("Redo", OnRedo, "cad.studio.redo");
        _deleteBtn = Btn("Delete", OnDelete, "cad.studio.delete");
        toolbar.Children.Add(_undoBtn);
        toolbar.Children.Add(_redoBtn);
        toolbar.Children.Add(_deleteBtn);
        toolbar.Children.Add(Sep());
        toolbar.Children.Add(SectionLabel("Workspace"));
        toolbar.Children.Add(Btn("Draft 2D", () => SetStudioWorkspace(StudioWorkspace.Draft2D), "cad.studio.ws.draft2d", "Plan drafting (XZ)"));
        toolbar.Children.Add(Btn("Draft 3D", () => SetStudioWorkspace(StudioWorkspace.Draft3D), "cad.studio.ws.draft3d", "Orbit wireframe drafting — Avalonia, not Raylib"));
        toolbar.Children.Add(Btn("Model", () => SetStudioWorkspace(StudioWorkspace.Model), "cad.studio.ws.model", "Bridged mesh scene"));
        toolbar.Children.Add(Btn("Stage", () => SetStudioWorkspace(StudioWorkspace.Stage), "cad.studio.ws.stage", "Lights / render"));
        toolbar.Children.Add(Btn("Ship", () => SetStudioWorkspace(StudioWorkspace.Ship), "cad.studio.ws.ship", "Ship authoring mode"));
        toolbar.Children.Add(Sep());
        _bridgeBtn = Btn("Bridge", OnBridge, "cad.studio.bridge", "Cad → Scene meshes");
        toolbar.Children.Add(_bridgeBtn);
        toolbar.Children.Add(Btn("Export Scene…", () => _ = OnExportSceneAsync(), "cad.studio.exportScene"));
        toolbar.Children.Add(Btn("Fit", FitActiveView, "cad.studio.fit"));

        _draftBarHost = BuildDraftOptionsBar();

        _modeBanner = new TextBlock
        {
            Margin = new Thickness(12, 2, 12, 6),
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = GraphicalProfile.TextBrush,
            Text = "Draft 2D — plan (XZ)",
        };
        AgentProperties.SetId(_modeBanner, "cad.studio.modeBanner");

        _cadEditor = new CadEditorSurface(_doc, _settings, _bus, _dispatcher, _tools, _modelRenderer);
        AgentProperties.SetId(_cadEditor.DraftViewport, "cad.studio.viewport.plan");
        AgentProperties.SetId(_cadEditor.Draft3DViewport, "cad.studio.viewport.draft3d");
        AgentProperties.SetId(_cadEditor.ModelHost, "cad.studio.viewport.preview");
        AgentProperties.SetId(_cadEditor.SceneTree, "cad.studio.sceneTree");
        AgentProperties.SetId(_cadEditor.PropertyPanel, "cad.studio.properties");

        _cadHost = BuildCadHost(_cadEditor, _draftBarHost);

        _shipEditor = new CadEditorSurface(
            _shipCad.Document,
            _shipCad.Settings,
            _shipCad.Bus,
            _shipCad.Dispatcher,
            _shipTools,
            new CadModelRenderer(_shipCad.Document, _shipCad.Settings));
        AgentProperties.SetId(_shipEditor.ModelHost, "cad.studio.ship.viewport.model");
        _shipCad.Editor = _shipEditor;
        var shipStatus = new TextBlock
        {
            Text = "PLAN",
            Margin = new Thickness(8, 4),
            Foreground = GraphicalProfile.TextBrush,
        };
        AgentProperties.SetId(shipStatus, "cad.studio.ship.status");
        _shipHost = ShipDesignChrome.CreateShell(_shipCad, _shipDesign, _shipEditor, shipStatus);
        _shipHost.IsVisible = false;

        _sceneEditor = new SceneEditorSurface(_scene, composeDefaultLayout: false);
        _sceneHost = BuildSceneHost(_sceneEditor);

        _host = new Panel();
        _host.Children.Add(_cadHost);
        _host.Children.Add(_sceneHost);
        _host.Children.Add(_shipHost);

        _commandBar = new StudioCommandBar();
        AgentProperties.SetId(_commandBar, "cad.studio.commandBar.host");
        if (_commandBar.Content is Border { Child: Panel commandRow })
        {
            foreach (var child in commandRow.Children)
            {
                if (child is TextBox input)
                    AgentProperties.SetId(input, "cad.studio.commandBar", AgentRoleNames.TextBox);
            }
        }

        _commandBar.PromptLabel = "Line(Point(0,1), Point(1,1)); Extrude(2.4); Snap(on); AxisLock(x);";
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
                SyncDraftOptionsUi();
                _cadEditor.Draft3DViewport.InvalidateVisual();
                _cadEditor.DraftViewport.InvalidateVisual();
            }

            _commandBar.PromptLabel = _tools.PromptHint;
        };
        _commandBar.Cancelled += (_, _) =>
        {
            _tools.Cancel();
            _commandBar.PromptLabel = _tools.PromptHint;
        };

        var topStack = new StackPanel { Spacing = 0 };
        topStack.Children.Add(toolbar);
        topStack.Children.Add(_modeBanner);

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        root.Children.Add(topStack);
        Grid.SetRow(_host, 1);
        root.Children.Add(_host);
        Grid.SetRow(_commandBar, 2);
        root.Children.Add(_commandBar);

        _portsLine = new TextBlock
        {
            Margin = new Thickness(10, 2),
            FontSize = 11,
            Opacity = 0.75,
            Foreground = GraphicalProfile.TextBrush,
            Text = PortStatusLine(),
            IsVisible = Program.CadSurface is not null || Program.SceneSurface is not null,
        };
        AgentProperties.SetId(_portsLine, "cad.studio.ports");

        var bottom = new StackPanel
        {
            Spacing = 0,
            Children = { chrome.FlashLine, chrome.StatusLine, _portsLine },
        };

        return new DockPanel
        {
            Children =
            {
                new Border
                {
                    [DockPanel.DockProperty] = Dock.Bottom,
                    Child = bottom,
                },
                root,
            },
        };
    }

    private Control BuildCadHost(CadEditorSurface editor, Control draftBar)
    {
        var left = new DockPanel { Margin = new Thickness(4), Width = 260 };
        var leftTitle = new TextBlock
        {
            Text = "Entities",
            FontWeight = FontWeight.SemiBold,
            FontSize = 13,
            Margin = new Thickness(6, 8, 6, 4),
            Foreground = GraphicalProfile.MutedBrush,
        };
        DockPanel.SetDock(leftTitle, Dock.Top);
        left.Children.Add(leftTitle);
        left.Children.Add(editor.SceneTree);

        var right = new DockPanel { Margin = new Thickness(4), Width = 280 };
        var rightTitle = new TextBlock
        {
            Text = "Properties",
            FontWeight = FontWeight.SemiBold,
            FontSize = 13,
            Margin = new Thickness(6, 8, 6, 4),
            Foreground = GraphicalProfile.MutedBrush,
        };
        DockPanel.SetDock(rightTitle, Dock.Top);
        right.Children.Add(rightTitle);
        right.Children.Add(editor.PropertyPanel);

        // Hide mesh-mode strips; Draft 2D/3D uses the drafting bar below.
        editor.SelectionModeBar.IsVisible = false;
        editor.ToolStrip.IsVisible = false;

        var center = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        center.Children.Add(draftBar);
        Grid.SetRow(editor, 1);
        center.Children.Add(editor);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("260,*,280") };
        Grid.SetColumn(left, 0);
        Grid.SetColumn(center, 1);
        Grid.SetColumn(right, 2);
        grid.Children.Add(left);
        grid.Children.Add(center);
        grid.Children.Add(right);
        return grid;
    }

    private Control BuildDraftOptionsBar()
    {
        var row = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(8, 4, 8, 6),
        };
        AgentProperties.SetId(row, "cad.studio.draftBar");

        row.Children.Add(SectionLabel("Units"));
        _unitCombo = new ComboBox
        {
            Width = 112,
            VerticalAlignment = VerticalAlignment.Center,
            ItemsSource = new[]
            {
                new UnitChoice(CadUnits.Meter, "Meters (m)"),
                new UnitChoice(CadUnits.Centimeter, "Centimeters (cm)"),
                new UnitChoice(CadUnits.Millimeter, "Millimeters (mm)"),
                new UnitChoice(CadUnits.Inch, "Inches (in)"),
            },
        };
        AgentProperties.SetId(_unitCombo, "cad.studio.units", AgentRoleNames.ComboBox);
        _unitCombo.SelectionChanged += (_, _) =>
        {
            if (_syncingDraftUi || _unitCombo.SelectedItem is not UnitChoice choice)
                return;
            _settings.Settings.DisplayUnit = choice.Id;
            InvalidateDraftViews();
            RefreshTitle();
        };
        row.Children.Add(_unitCombo);
        row.Children.Add(Sep());

        row.Children.Add(SectionLabel("Tools"));
        row.Children.Add(Btn("Select", () => ExecTool("select"), "cad.studio.tool.select"));
        row.Children.Add(Btn("Line", () => ExecTool("line"), "cad.studio.tool.line", "L"));
        row.Children.Add(Btn("Circle", () => ExecTool("circle"), "cad.studio.tool.circle", "C"));
        row.Children.Add(Btn("Rect", () => ExecTool("rect"), "cad.studio.tool.rect", "R"));
        row.Children.Add(Btn("Wall", () => ExecTool("wall"), "cad.studio.tool.wall", "W"));
        row.Children.Add(Btn("Dim", () => ExecTool("dimension"), "cad.studio.tool.dimension"));
        row.Children.Add(Btn("Box", () => ExecPrompt("Box(1,1,1)"), "cad.studio.tool.box"));
        row.Children.Add(Btn("Extrude", () => ExecPrompt("Extrude(2.4)"), "cad.studio.tool.extrude"));
        row.Children.Add(Sep());

        row.Children.Add(SectionLabel("Snap"));
        _snapCheck = new CheckBox
        {
            Content = "Snap to grid",
            IsChecked = _settings.Settings.SnapToGrid,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 8, 0),
        };
        AgentProperties.SetId(_snapCheck, "cad.studio.snap", AgentRoleNames.CheckBox);
        _snapCheck.IsCheckedChanged += (_, _) =>
        {
            if (_syncingDraftUi)
                return;
            _cad.Execute(new CadCommandDto
            {
                ActionId = CadSessionActionIds.SetSnap,
                Snap = _snapCheck.IsChecked == true,
            });
            InvalidateDraftViews();
        };
        row.Children.Add(_snapCheck);

        row.Children.Add(SectionLabel("Grid"));
        _gridCombo = new ComboBox
        {
            Width = 96,
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
        AgentProperties.SetId(_gridCombo, "cad.studio.grid", AgentRoleNames.ComboBox);
        _gridCombo.SelectionChanged += (_, _) =>
        {
            if (_syncingDraftUi)
                return;
            if (_gridCombo.SelectedItem is not GridChoice g)
                return;
            _cad.Execute(new CadCommandDto
            {
                ActionId = CadSessionActionIds.SetGrid,
                GridStep = g.Step,
            });
            InvalidateDraftViews();
        };
        row.Children.Add(_gridCombo);
        row.Children.Add(Sep());

        _continuousCheck = new CheckBox
        {
            Content = "Continuous",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 8, 0),
        };
        ToolTip.SetTip(_continuousCheck, "Line: chain from the last endpoint (Esc ends)");
        AgentProperties.SetId(_continuousCheck, "cad.studio.continuous", AgentRoleNames.CheckBox);
        _continuousCheck.IsCheckedChanged += (_, _) =>
        {
            if (_syncingDraftUi)
                return;
            _tools.ContinuousLine = _continuousCheck.IsChecked == true;
            _settings.Settings.ContinuousLine = _tools.ContinuousLine;
            _settings.Save();
            _commandBar.PromptLabel = _tools.PromptHint;
        };
        row.Children.Add(_continuousCheck);

        row.Children.Add(SectionLabel("Level"));
        _elevationBox = new NumericUpDown
        {
            Width = 88,
            Minimum = -1000,
            Maximum = 1000,
            Increment = 0.5m,
            FormatString = "0.##",
            VerticalAlignment = VerticalAlignment.Center,
        };
        AgentProperties.SetId(_elevationBox, "cad.studio.elevation", AgentRoleNames.TextBox);
        ToolTip.SetTip(_elevationBox, "Drawing plane elevation (world Y)");
        _elevationBox.ValueChanged += (_, e) =>
        {
            if (_syncingDraftUi || e.NewValue is null)
                return;
            _settings.Settings.DrawElevation = (float)e.NewValue.Value;
            InvalidateDraftViews();
            RefreshTitle();
        };
        row.Children.Add(_elevationBox);
        row.Children.Add(Btn("+1", () => NudgeElevation(1f), "cad.studio.elevation.up", "Next level / +1 m"));
        row.Children.Add(Btn("−1", () => NudgeElevation(-1f), "cad.studio.elevation.down", "Previous level / −1 m"));
        row.Children.Add(Btn("0", () => SetElevation(0f), "cad.studio.elevation.zero"));

        _isolateCheck = new CheckBox
        {
            Content = "Isolate level",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 8, 0),
        };
        ToolTip.SetTip(_isolateCheck, "Dim and skip hit testing for entities away from the current level");
        AgentProperties.SetId(_isolateCheck, "cad.studio.isolate", AgentRoleNames.CheckBox);
        _isolateCheck.IsCheckedChanged += (_, _) =>
        {
            if (_syncingDraftUi)
                return;
            _settings.Settings.IsolateLevel = _isolateCheck.IsChecked == true;
            InvalidateDraftViews();
            RefreshTitle();
        };
        row.Children.Add(_isolateCheck);
        row.Children.Add(Sep());

        row.Children.Add(SectionLabel("Axis lock"));
        _lockNone = AxisLockBtn("Free", "none", "cad.studio.axis.none");
        _lockX = AxisLockBtn("X", "x", "cad.studio.axis.x");
        _lockY = AxisLockBtn("Y", "y", "cad.studio.axis.y");
        _lockZ = AxisLockBtn("Z", "z", "cad.studio.axis.z");
        row.Children.Add(_lockNone);
        row.Children.Add(_lockX);
        row.Children.Add(_lockY);
        row.Children.Add(_lockZ);

        SyncDraftOptionsUi();
        return new Border
        {
            Background = GraphicalProfile.SurfaceBrush,
            BorderBrush = GraphicalProfile.BorderBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = row,
        };
    }

    private Button AxisLockBtn(string label, string axis, string agentId)
    {
        var b = Btn(label, () =>
        {
            _cad.Execute(new CadCommandDto
            {
                ActionId = CadSessionActionIds.SetAxisLock,
                Kind = axis,
            });
            SyncDraftOptionsUi();
            InvalidateDraftViews();
        }, agentId, $"Lock move to {axis} axis");
        return b;
    }

    private void SyncDraftOptionsUi()
    {
        if (_snapCheck is null
            || _gridCombo is null
            || _unitCombo is null
            || _continuousCheck is null
            || _elevationBox is null
            || _isolateCheck is null)
            return;

        _syncingDraftUi = true;
        try
        {
            if (_unitCombo.ItemsSource is IEnumerable<UnitChoice> units)
            {
                _unitCombo.SelectedItem = units.FirstOrDefault(
                    unit => string.Equals(
                        unit.Id,
                        _settings.Settings.DisplayUnit,
                        StringComparison.OrdinalIgnoreCase))
                    ?? units.FirstOrDefault();
            }

            _snapCheck.IsChecked = _settings.Settings.SnapToGrid;
            _continuousCheck.IsChecked = _settings.Settings.ContinuousLine;
            _tools.ContinuousLine = _settings.Settings.ContinuousLine;
            _elevationBox.Value = (decimal)_settings.Settings.DrawElevation;
            _isolateCheck.IsChecked = _settings.Settings.IsolateLevel;
            var step = _settings.Settings.GridStep;
            if (_gridCombo.ItemsSource is IEnumerable<GridChoice> choices)
            {
                GridChoice? match = null;
                foreach (var c in choices)
                {
                    if (System.Math.Abs(c.Step - step) < 1e-4f)
                    {
                        match = c;
                        break;
                    }
                }

                if (match is not null)
                    _gridCombo.SelectedItem = match;
                else if (_gridCombo.SelectedItem is null)
                    _gridCombo.SelectedIndex = 2;
            }

            var axis = _settings.Settings.AxisLock.Trim().ToLowerInvariant();
            StyleAxis(_lockNone, axis is "none" or "");
            StyleAxis(_lockX, axis == "x");
            StyleAxis(_lockY, axis == "y");
            StyleAxis(_lockZ, axis == "z");
        }
        finally
        {
            _syncingDraftUi = false;
        }
    }

    private static void StyleAxis(Button b, bool on)
    {
        b.FontWeight = on ? FontWeight.Bold : FontWeight.Normal;
        b.Background = on ? GraphicalProfile.AccentFillBrush : GraphicalProfile.RaisedBrush;
        b.Foreground = on ? GraphicalProfile.OnAccentFillBrush : GraphicalProfile.TextBrush;
    }

    private void InvalidateDraftViews()
    {
        if (_cadEditor is null)
            return;
        _cadEditor.DraftViewport.InvalidateVisual();
        _cadEditor.Draft3DViewport.InvalidateVisual();
    }

    private void ExecTool(string tool) =>
        _cad.Execute(new CadCommandDto { ActionId = CadSessionActionIds.SetTool, Tool = tool });

    private void ExecPrompt(string prompt)
    {
        var result = _cad.Execute(new CadCommandDto
        {
            ActionId = CadSessionActionIds.RunCommand,
            Prompt = prompt,
        });
        if (!result.Ok)
            _feedback.FlashError(result.Message);
        else
        {
            _feedback.SetStatus($"OK — {prompt}");
            InvalidateDraftViews();
        }
    }

    private async Task OnSaveAsAsync()
    {
        if (_workspace == StudioWorkspace.Ship)
        {
            await OnSaveShipAsAsync();
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save CADJSON As",
            SuggestedFileName = Path.GetFileName(_doc.DocumentPath),
            DefaultExtension = "cadjson",
            FileTypeChoices =
            [
                new FilePickerFileType("CADJSON") { Patterns = ["*.cadjson"] },
            ],
        });
        var path = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
            return;

        if (!path.EndsWith(".cadjson", StringComparison.OrdinalIgnoreCase)
            && !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            path += ".cadjson";
        }

        try
        {
            _doc.SaveTo(path);
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
        if (_workspace == StudioWorkspace.Ship)
        {
            _feedback.FlashError("CAD Phys export is available in generic CAD modes.");
            return;
        }

        var suggested = Path.ChangeExtension(
                            Path.GetFileName(_doc.DocumentPath),
                            ".cadphys.json")
                        ?? "cad-document.cadphys.json";
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export CAD Phys JSON",
            SuggestedFileName = suggested,
            DefaultExtension = "cadphys.json",
            FileTypeChoices =
            [
                new FilePickerFileType("CAD Phys JSON") { Patterns = ["*.cadphys.json", "*.json"] },
            ],
        });
        var path = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
            return;

        var result = _cad.Execute(new CadCommandDto
        {
            ActionId = CadSessionActionIds.ExportPhys,
            Path = path,
        });
        if (result.Ok)
            _feedback.Flash(result.Message);
        else
            _feedback.FlashError(result.Message);
    }

    private async Task OnDumpArtifactsAsync()
    {
        if (_workspace == StudioWorkspace.Ship)
        {
            _feedback.FlashError("Artifact dumps are available in generic CAD modes.");
            return;
        }

        if (_dumpBusy)
            return;

        _dumpBusy = true;
        try
        {
            var previous = _workspace;
            var result = await _artifacts.DumpAllAsync(
                this,
                _cadEditor.DraftViewport,
                _cadEditor.ModelHost,
                ensureModelViewAsync: async () =>
                {
                    _cadHost.IsVisible = true;
                    _sceneHost.IsVisible = false;
                    _cad.Execute(new CadCommandDto
                    {
                        ActionId = CadSessionActionIds.SetWorkspace,
                        Workspace = CadWorkspaceMapping.ToStorage(CadWorkspace.Preview),
                    });
                    await Task.Delay(80);
                },
                ensureDraftViewAsync: async () =>
                {
                    SetStudioWorkspace(StudioWorkspace.Draft2D);
                    await Task.Delay(40);
                });

            SetStudioWorkspace(previous);
            var outputs = new List<string> { $"doc={Path.GetFileName(result.DocumentPath)}" };
            if (result.DraftPngPath is not null)
                outputs.Add("draft.png");
            if (result.ModelPngPath is not null)
                outputs.Add("model.png");
            if (result.WindowPngPath is not null)
                outputs.Add("window.png");
            _feedback.Flash($"Dump → {_artifacts.DumpsDirectory} ({string.Join(", ", outputs)})");
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

    private void SetElevation(float elevation)
    {
        _settings.Settings.DrawElevation = elevation;
        SyncDraftOptionsUi();
        InvalidateDraftViews();
        RefreshTitle();
    }

    private void NudgeElevation(float delta) =>
        SetElevation(_settings.Settings.DrawElevation + delta);

    private void SetStudioWorkspace(StudioWorkspace workspace)
    {
        var shipMode = workspace == StudioWorkspace.Ship;
        _workspace = workspace;
        var sceneMode = IsSceneWorkspace(workspace);
        if (shipMode)
        {
            if (_scenePresenting)
            {
                _sceneEditor.StopPresenting();
                _scenePresenting = false;
            }

            _cadHost.IsVisible = false;
            _sceneHost.IsVisible = false;
            _shipHost.IsVisible = true;
            _commandBar.IsVisible = false;
            _shipAttachment ??= ShipDesignChrome.Attach(_shipCad, _shipDesign);
            _modeBanner.Text = "Ship — PLAN / MODEL / ANALYZE · .shipjson is authoritative";
            RefreshModeChrome();
            RefreshTitle();
            _feedback.SetStatus(
                $"Ship · active={(_shipDesign.Path is null ? "new .shipjson" : Path.GetFileName(_shipDesign.Path))}");
            return;
        }

        if (_shipAttachment is not null)
        {
            _shipAttachment.Dispose();
            _shipAttachment = null;
        }

        _cad.Editor = _cadEditor;
        _commandBar.IsVisible = !sceneMode;
        _cadHost.IsVisible = !sceneMode;
        _sceneHost.IsVisible = sceneMode;
        _shipHost.IsVisible = false;

        _modeBanner.Text = workspace switch
        {
            StudioWorkspace.Draft2D => "Draft 2D — plan (XZ) · click to draw · snap & grid in the bar above the viewport",
            StudioWorkspace.Draft3D => "Draft 3D — Avalonia box-grid wireframe (no Raylib) · MMB orbit · drag to move · axis lock X/Y/Z",
            StudioWorkspace.Model => "Model — bridged mesh scene (Raylib present)",
            StudioWorkspace.Stage => "Stage — lights / materials / render",
            _ => StudioWorkspaceIds.ToDisplay(workspace),
        };

        if (!sceneMode)
        {
            if (_scenePresenting)
            {
                _sceneEditor.StopPresenting();
                _scenePresenting = false;
            }

            var cadWs = workspace == StudioWorkspace.Draft3D ? CadWorkspace.Modeling : CadWorkspace.Cad;
            _cad.Execute(new CadCommandDto
            {
                ActionId = CadSessionActionIds.SetWorkspace,
                Workspace = CadWorkspaceMapping.ToStorage(cadWs),
            });
            if (workspace == StudioWorkspace.Draft3D)
            {
                _cadEditor.Draft3DViewport.Fit();
                SyncDraftOptionsUi();
            }
        }
        else
        {
            EnsureSceneFromCad(force: _bridgeDirty || _scene.Document.Nodes.Count == 0);
            if (!_scenePresenting)
            {
                _sceneEditor.StartPresenting();
                _scenePresenting = true;
            }

            if (workspace == StudioWorkspace.Stage)
            {
                _scene.Execute(new AgentCommand { ActionId = SceneSessionActionIds.EnsureStudioLights });
            }
        }

        RefreshModeChrome();
        RefreshTitle();
        _feedback.SetStatus($"{StudioWorkspaceIds.ToDisplay(workspace)}  ·  active={(sceneMode ? "Scene (.nov3djson)" : "Cad (.cadjson)")}");
    }

    private void RefreshModeChrome()
    {
        var ship = _workspace == StudioWorkspace.Ship;
        var scene = IsSceneWorkspace(_workspace);
        var draft = !ship && !scene;
        _exportPhysBtn.IsVisible = draft;
        _dumpBtn.IsVisible = draft;
        _undoBtn.IsVisible = draft;
        _redoBtn.IsVisible = draft;
        _deleteBtn.IsVisible = draft;
        _bridgeBtn.IsVisible = !ship;
        _draftBarHost.IsVisible = draft;
        _commandBar.IsVisible = draft;
        _portsLine.IsVisible = Program.CadSurface is not null || Program.SceneSurface is not null;
    }

    private void OnUndo()
    {
        if (_workspace != StudioWorkspace.Draft2D && _workspace != StudioWorkspace.Draft3D)
            return;
        _cad.Execute(new CadCommandDto { ActionId = CadSessionActionIds.Undo });
    }

    private void OnRedo()
    {
        if (_workspace != StudioWorkspace.Draft2D && _workspace != StudioWorkspace.Draft3D)
            return;
        _cad.Execute(new CadCommandDto { ActionId = CadSessionActionIds.Redo });
    }

    private void OnDelete()
    {
        if (_workspace != StudioWorkspace.Draft2D && _workspace != StudioWorkspace.Draft3D)
            return;
        _cad.Execute(new CadCommandDto { ActionId = CadSessionActionIds.DeleteSelection });
    }

    private void FitActiveView()
    {
        if (_workspace == StudioWorkspace.Ship)
        {
            if (_shipDesign.Workspace == ShipWorkspaceKind.Model)
                _shipEditor.Fit();
            return;
        }

        if (IsSceneWorkspace(_workspace))
            _scene.Execute(new AgentCommand { ActionId = SceneSessionActionIds.Fit });
        else
            _cad.Execute(new CadCommandDto { ActionId = CadSessionActionIds.Fit });
    }

    private static TextBlock SectionLabel(string text) => new()
    {
        Text = text,
        FontSize = 11,
        FontWeight = FontWeight.SemiBold,
        Opacity = 0.65,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(4, 0, 6, 0),
        Foreground = GraphicalProfile.MutedBrush,
    };

    private sealed record UnitChoice(string Id, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record GridChoice(float Step, string Label)
    {
        public override string ToString() => Label;
    }

    private Control BuildSceneHost(SceneEditorSurface surface)
    {
        var rightRail = new ScrollViewer
        {
            Width = 300,
            Content = new StackPanel
            {
                Children =
                {
                    surface.MeshAttributes,
                    surface.ModifierStack,
                    surface.Properties,
                },
            },
        };

        var center = new Grid { ColumnDefinitions = new ColumnDefinitions("260,*,300") };
        Grid.SetColumn(surface.ObjectManager, 0);
        Grid.SetColumn(surface.Viewport, 1);
        Grid.SetColumn(rightRail, 2);
        center.Children.Add(surface.ObjectManager);
        center.Children.Add(surface.Viewport);
        center.Children.Add(rightRail);

        var chrome = new Border
        {
            Background = GraphicalProfile.SurfaceBrush,
            BorderBrush = GraphicalProfile.BorderBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = surface.CreateChrome(Path.Combine(_settings.DataRoot, "dumps")),
            [DockPanel.DockProperty] = Dock.Top,
        };

        return new DockPanel
        {
            Background = GraphicalProfile.BackgroundBrush,
            Children = { chrome, center },
        };
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        _cad.Editor = _cadEditor;
        _doc.OpenOrCreateDefault();
        SyncDraftOptionsUi();
        SetStudioWorkspace(StudioWorkspace.Draft2D);
        RefreshTitle();
        _feedback.SetStatus("Command: Line(Point(0,1), Point(1,1)); Circle(Point(2,2), 0.5); Extrude(2.4); Snap(on); AxisLock(x);");
        if (_migration.Failures.Count > 0)
            _feedback.FlashError($"Legacy data migration incomplete; originals were not changed. See {_migration.ManifestPath}");
        else if (_migration.FilesCopied > 0)
            _feedback.Flash(
                $"Imported {_migration.FilesCopied} legacy file(s) without conversion. Original formats remain under {_migration.ManifestPath}");
        _commandBar.FocusInput();
    }

    private void OnBridge()
    {
        if (_workspace == StudioWorkspace.Ship)
        {
            _feedback.FlashError("Use Ship mode's Export scene action for ship evaluation.");
            return;
        }

        var result = _cad.Execute(new CadCommandDto { ActionId = CadSessionActionIds.BridgeScene });
        if (!result.Ok)
            _feedback.FlashError(result.Message);
        else
        {
            _feedback.Flash(result.Message);
            SetStudioWorkspace(StudioWorkspace.Model);
        }
    }

    private void OnSceneBridged(SceneDocument scene)
    {
        _scene.ReplaceDocument(scene);
        _bridgeDirty = false;
        RefreshTitle();
    }

    private void EnsureSceneFromCad(bool force)
    {
        if (!force && !_bridgeDirty)
            return;

        var scene = CadSceneBridge.ToSceneDocument(_doc.Document, new CadSceneBridgeOptions
        {
            EnsureStudioLights = true,
        });
        _scene.ReplaceDocument(scene);
        _bridgeDirty = false;
    }

    private async Task OnExportSceneAsync()
    {
        if (_workspace == StudioWorkspace.Ship)
        {
            if (!_shipDesign.HasShip)
            {
                _feedback.FlashError("Create a ship before exporting a scene.");
                return;
            }

            var shipScenePath = Path.Combine(
                _shipDesign.DataRoot,
                "exports",
                "ship-analyze.nov3djson");
            Directory.CreateDirectory(Path.GetDirectoryName(shipScenePath)!);
            var evaluation = ShipDesignEvaluator.Evaluate(_shipDesign.Design, shipScenePath);
            _feedback.Flash(
                $"Scene export · {evaluation.MeshNodeCount} meshes · {evaluation.CutoutCount} cutouts → {shipScenePath}");
            return;
        }

        var suggested = Path.ChangeExtension(Path.GetFileName(_doc.DocumentPath), ".nov3djson") ?? "studio.nov3djson";
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Scene (.nov3djson)",
            SuggestedFileName = suggested,
            DefaultExtension = "nov3djson",
            FileTypeChoices =
            [
                new FilePickerFileType("Novolis Scene") { Patterns = ["*.nov3djson"] },
            ],
        });
        var path = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
            return;

        var result = _cad.Execute(new CadCommandDto
        {
            ActionId = CadSessionActionIds.ExportScene,
            Path = path,
        });
        if (result.Ok)
            _feedback.Flash(result.Message);
        else
            _feedback.FlashError(result.Message);
    }

    private void OnSave()
    {
        if (_workspace == StudioWorkspace.Ship)
        {
            _shipDesign.Save();
            _feedback.Flash(
                _shipDesign.Path is { } shipPath
                    ? $"Saved {Path.GetFileName(shipPath)}"
                    : "Create a ship before saving.");
            RefreshTitle();
            return;
        }

        if (IsSceneWorkspace(_workspace))
        {
            var path = _scene.DocumentPath
                       ?? Path.Combine(_settings.DataRoot, "exports", "bridged.nov3djson");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var result = _scene.Execute(new AgentCommand { ActionId = SceneSessionActionIds.Save, Path = path });
            if (result.Ok)
                _feedback.Flash(result.Message);
            else
                _feedback.FlashError(result.Message);
            return;
        }

        var cad = _cad.Execute(new CadCommandDto { ActionId = CadSessionActionIds.Save });
        if (cad.Ok)
            _feedback.Flash(cad.Message);
        else
            _feedback.FlashError(cad.Message);
    }

    private async Task OnNewAsync()
    {
        if (_workspace == StudioWorkspace.Ship)
        {
            _shipDesign.ClearToBlank();
            _feedback.Flash("New ship design");
            RefreshTitle();
            await Task.CompletedTask;
            return;
        }

        _cad.Execute(new CadCommandDto { ActionId = CadSessionActionIds.New });
        _bridgeDirty = true;
        SetStudioWorkspace(StudioWorkspace.Draft2D);
        _feedback.Flash("New Cad document");
        await Task.CompletedTask;
    }

    private async Task OnOpenAsync()
    {
        if (_workspace == StudioWorkspace.Ship)
        {
            await OnOpenShipAsync();
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open CAD or Ship",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("CAD or Ship") { Patterns = ["*.cadjson", "*.shipjson", "*.json"] },
                new FilePickerFileType("CadJSON") { Patterns = ["*.cadjson", "*.json"] },
                new FilePickerFileType("Ship JSON") { Patterns = ["*.shipjson"] },
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

        if (path.EndsWith(".shipjson", StringComparison.OrdinalIgnoreCase))
        {
            OpenShipPath(path);
            return;
        }

        var result = _cad.Execute(new CadCommandDto { ActionId = CadSessionActionIds.Open, Path = path });
        if (!result.Ok)
            _feedback.FlashError(result.Message);
        else
        {
            _bridgeDirty = true;
            SetStudioWorkspace(StudioWorkspace.Draft2D);
            _feedback.Flash(result.Message);
        }
    }

    private async Task OnOpenShipAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Ship JSON",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Ship JSON") { Patterns = ["*.shipjson"] },
            ],
        });
        if (files.Count == 0)
            return;

        var path = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            _feedback.FlashError("Could not open ship file");
            return;
        }

        OpenShipPath(path);
    }

    private void OpenShipPath(string path)
    {
        try
        {
            _shipDesign.OpenFromPath(path);
            SetStudioWorkspace(StudioWorkspace.Ship);
            _feedback.Flash($"Opened {Path.GetFileName(path)}");
            RefreshTitle();
        }
        catch (Exception ex)
        {
            _feedback.FlashError($"Open ship failed: {ex.Message}");
        }
    }

    private async Task OnSaveShipAsAsync()
    {
        var suggested = _shipDesign.HasShip
            ? $"{_shipDesign.Design.Ship.Name}.shipjson"
            : "ship.shipjson";
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Ship JSON As",
            SuggestedFileName = suggested,
            DefaultExtension = "shipjson",
            FileTypeChoices =
            [
                new FilePickerFileType("Ship JSON") { Patterns = ["*.shipjson"] },
            ],
        });
        var path = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
            return;

        if (!path.EndsWith(".shipjson", StringComparison.OrdinalIgnoreCase))
            path += ".shipjson";

        _shipDesign.SaveTo(path);
        _feedback.Flash($"Saved {Path.GetFileName(path)}");
        RefreshTitle();
    }

    private void RefreshTitle()
    {
        var dirty = _workspace == StudioWorkspace.Ship
            ? _shipDesign.IsDirty
            : _doc.IsDirty;
        var active = _workspace == StudioWorkspace.Ship
            ? (_shipDesign.Path is { } shipPath
                ? Path.GetFileName(shipPath)
                : _shipDesign.HasShip ? _shipDesign.Design.Ship.Name : "(untitled)")
            : IsSceneWorkspace(_workspace)
                ? (_scene.DocumentPath is { } scenePath
                    ? Path.GetFileName(scenePath)
                    : _scene.Document.Name + " (bridged)")
                : Path.GetFileName(_doc.DocumentPath);
        Title = $"Novolis CAD Studio — {StudioWorkspaceIds.ToDisplay(_workspace)} — {active}{(dirty ? " *" : "")}";
    }

    private static string PortStatusLine()
    {
        var cad = Program.CadSurface?.HttpBaseUrl is { } c
            ? $"Cad HTTP {c} TCP :{Program.CadSurface.TcpPort}"
            : "Cad session off";
        var scene = Program.SceneSurface?.HttpBaseUrl is { } s
            ? $"Scene HTTP {s} TCP :{Program.SceneSurface.TcpPort}"
            : "Scene session off";
        return $"{cad}  ·  {scene}";
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Source is TextBox or NumericUpDown)
            return;

        if (e.Key == Key.S
            && e.KeyModifiers.HasFlag(KeyModifiers.Control)
            && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            _ = OnSaveAsAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.S && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            OnSave();
            e.Handled = true;
        }
        else if (e.Key == Key.N && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _ = OnNewAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.O && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _ = OnOpenAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.F && !e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            FitActiveView();
            e.Handled = true;
        }
        else if (IsDraftWorkspace(_workspace)
                 && e.Key == Key.X
                 && !e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _cad.Execute(new CadCommandDto { ActionId = CadSessionActionIds.SetAxisLock, Kind = "x" });
            SyncDraftOptionsUi();
            InvalidateDraftViews();
            e.Handled = true;
        }
        else if (IsDraftWorkspace(_workspace)
                 && e.Key == Key.Y
                 && !e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _cad.Execute(new CadCommandDto { ActionId = CadSessionActionIds.SetAxisLock, Kind = "y" });
            SyncDraftOptionsUi();
            InvalidateDraftViews();
            e.Handled = true;
        }
        else if (IsDraftWorkspace(_workspace)
                 && e.Key == Key.Z
                 && !e.KeyModifiers.HasFlag(KeyModifiers.Control)
                 && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            _cad.Execute(new CadCommandDto { ActionId = CadSessionActionIds.SetAxisLock, Kind = "z" });
            SyncDraftOptionsUi();
            InvalidateDraftViews();
            e.Handled = true;
        }
    }

    private static bool IsSceneWorkspace(StudioWorkspace w) =>
        w is StudioWorkspace.Model or StudioWorkspace.Stage;

    private static bool IsDraftWorkspace(StudioWorkspace w) =>
        w is StudioWorkspace.Draft2D or StudioWorkspace.Draft3D;

    private static Button Btn(string text, Action action, string agentId, string? tip = null)
    {
        var b = new Button { Content = text, Padding = new Thickness(10, 4), Margin = new Thickness(0, 2) };
        AgentProperties.SetId(b, agentId, AgentRoleNames.Button);
        if (tip is not null)
            ToolTip.SetTip(b, tip);
        b.Click += (_, _) => action();
        return b;
    }

    private static Control Sep() => new Border { Width = 10 };
}