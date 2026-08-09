using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Novolis.Agent.Core;
using Novolis.Avalonia._3D;
using Novolis.Avalonia._3D.Session;
using Novolis.Avalonia._3D.Ui;
using Novolis.Avalonia.Agent;
using Novolis.Avalonia.Agent.Protocol;
using Novolis.Avalonia.Cad.Commands;
using Novolis.Avalonia.Cad.Core;
using Novolis.Avalonia.Cad.Services;
using Novolis.Avalonia.Cad.Session;
using Novolis.Avalonia.Cad.Ui;
using Novolis.Avalonia.Studio;
using Novolis.Cad.Primitives;
using Novolis.Cad.SceneBridge;
using Novolis._3D;

namespace CadStudio3D;

internal sealed class MainWindow : Window
{
    private readonly CadSessionService _cad;
    private readonly SceneSessionService _scene;
    private readonly CadDocumentSession _doc;
    private readonly CadEditorSettings _settings;
    private readonly CadCommandBus _bus;
    private readonly CadCommandDispatcher _dispatcher;
    private readonly CadToolController _tools;
    private readonly CadModelRenderer _modelRenderer;

    private CadEditorSurface _cadEditor = null!;
    private SceneEditorSurface _sceneEditor = null!;
    private Panel _host = null!;
    private Control _cadHost = null!;
    private Control _sceneHost = null!;
    private StudioFeedback _feedback = null!;
    private StudioCommandBar _commandBar = null!;
    private CheckBox _snapCheck = null!;
    private ComboBox _gridCombo = null!;
    private Button _lockNone = null!;
    private Button _lockX = null!;
    private Button _lockY = null!;
    private Button _lockZ = null!;
    private TextBlock _modeBanner = null!;
    private StudioWorkspace _workspace = StudioWorkspace.Draft2D;
    private bool _scenePresenting;
    private bool _bridgeDirty = true;
    private bool _syncingDraftUi;

    public MainWindow(CadSessionService cad, SceneSessionService scene)
    {
        _cad = cad;
        _scene = scene;
        _doc = cad.Document;
        _settings = cad.Settings;
        _bus = cad.Bus;
        _dispatcher = cad.Dispatcher;
        _tools = new CadToolController(_dispatcher, _settings);
        _modelRenderer = new CadModelRenderer(_doc, _settings);
        _cad.ExportRoot = Path.Combine(_settings.DataRoot, "exports");
        _cad.FitHandler = () => _cad.Execute(new CadCommandDto { ActionId = CadSessionActionIds.Fit });

        Title = "Novolis CAD Studio 3D";
        Width = 1480;
        Height = 920;
        MinWidth = 1100;
        MinHeight = 640;
        Background = new SolidColorBrush(Color.FromRgb(14, 20, 28));

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
            _settings.Save();
        };
        KeyDown += OnKeyDown;
    }

    private Control BuildLayout()
    {
        var chrome = StudioChrome.Create();
        _feedback = chrome.CreateFeedback();
        AgentProperties.SetId(chrome.StatusLine, "cad3d.status");
        AgentProperties.SetId(chrome.FlashLine, "cad3d.flash");

        var toolbar = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(10, 8, 10, 4),
        };
        AgentProperties.SetId(toolbar, "cad3d.toolbar");

        toolbar.Children.Add(SectionLabel("File"));
        toolbar.Children.Add(Btn("New", () => _ = OnNewAsync(), "cad3d.tool.new"));
        toolbar.Children.Add(Btn("Open…", () => _ = OnOpenAsync(), "cad3d.tool.open"));
        toolbar.Children.Add(Btn("Save", OnSave, "cad3d.tool.save"));
        toolbar.Children.Add(Sep());
        toolbar.Children.Add(SectionLabel("Edit"));
        toolbar.Children.Add(Btn("Undo", () => _cad.Execute(new CadCommandDto { ActionId = CadSessionActionIds.Undo }), "cad3d.undo"));
        toolbar.Children.Add(Btn("Redo", () => _cad.Execute(new CadCommandDto { ActionId = CadSessionActionIds.Redo }), "cad3d.redo"));
        toolbar.Children.Add(Btn("Delete", () => _cad.Execute(new CadCommandDto { ActionId = CadSessionActionIds.DeleteSelection }), "cad3d.delete"));
        toolbar.Children.Add(Sep());
        toolbar.Children.Add(SectionLabel("Workspace"));
        toolbar.Children.Add(Btn("Draft 2D", () => SetStudioWorkspace(StudioWorkspace.Draft2D), "cad3d.ws.draft2d", "Plan drafting (XZ)"));
        toolbar.Children.Add(Btn("Draft 3D", () => SetStudioWorkspace(StudioWorkspace.Draft3D), "cad3d.ws.draft3d", "Orbit wireframe drafting — Avalonia, not Raylib"));
        toolbar.Children.Add(Btn("Model", () => SetStudioWorkspace(StudioWorkspace.Model), "cad3d.ws.model", "Bridged mesh scene"));
        toolbar.Children.Add(Btn("Stage", () => SetStudioWorkspace(StudioWorkspace.Stage), "cad3d.ws.stage", "Lights / render"));
        toolbar.Children.Add(Sep());
        toolbar.Children.Add(Btn("Bridge", OnBridge, "cad3d.bridge", "Cad → Scene meshes"));
        toolbar.Children.Add(Btn("Export Scene…", () => _ = OnExportSceneAsync(), "cad3d.exportScene"));
        toolbar.Children.Add(Btn("Fit", () =>
        {
            if (IsSceneWorkspace(_workspace))
                _scene.Execute(new AgentCommand { ActionId = SceneSessionActionIds.Fit });
            else
                _cad.Execute(new CadCommandDto { ActionId = CadSessionActionIds.Fit });
        }, "cad3d.fit"));

        var draftBar = BuildDraftOptionsBar();

        _modeBanner = new TextBlock
        {
            Margin = new Thickness(12, 2, 12, 6),
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(200, 220, 235)),
            Text = "Draft 2D — plan (XZ)",
        };
        AgentProperties.SetId(_modeBanner, "cad3d.modeBanner");

        _cadEditor = new CadEditorSurface(_doc, _settings, _bus, _dispatcher, _tools, _modelRenderer);
        AgentProperties.SetId(_cadEditor.DraftViewport, "cad3d.viewport.plan");
        AgentProperties.SetId(_cadEditor.Draft3DViewport, "cad3d.viewport.draft3d");
        AgentProperties.SetId(_cadEditor.ModelHost, "cad3d.viewport.preview");
        AgentProperties.SetId(_cadEditor.SceneTree, "cad3d.sceneTree");
        AgentProperties.SetId(_cadEditor.PropertyPanel, "cad3d.properties");

        _cadHost = BuildCadHost(_cadEditor, draftBar);

        _sceneEditor = new SceneEditorSurface(_scene, composeDefaultLayout: false);
        _sceneHost = BuildSceneHost(_sceneEditor);

        _host = new Panel();
        _host.Children.Add(_cadHost);
        _host.Children.Add(_sceneHost);

        _commandBar = new StudioCommandBar();
        AgentProperties.SetId(_commandBar, "cad3d.commandBar.host");
        if (_commandBar.Content is Border { Child: Panel commandRow })
        {
            foreach (var child in commandRow.Children)
            {
                if (child is TextBox input)
                    AgentProperties.SetId(input, "cad3d.commandBar", AgentRoleNames.TextBox);
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

        var ports = new TextBlock
        {
            Margin = new Thickness(10, 2),
            FontSize = 11,
            Opacity = 0.75,
            Foreground = Brushes.WhiteSmoke,
            Text = PortStatusLine(),
        };
        AgentProperties.SetId(ports, "cad3d.ports");

        var bottom = new StackPanel
        {
            Spacing = 0,
            Children = { chrome.FlashLine, chrome.StatusLine, ports },
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
            Foreground = new SolidColorBrush(Color.FromRgb(180, 200, 215)),
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
            Foreground = new SolidColorBrush(Color.FromRgb(180, 200, 215)),
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
        AgentProperties.SetId(row, "cad3d.draftBar");

        row.Children.Add(SectionLabel("Tools"));
        row.Children.Add(Btn("Select", () => ExecTool("select"), "cad3d.tool.select"));
        row.Children.Add(Btn("Line", () => ExecTool("line"), "cad3d.tool.line", "L"));
        row.Children.Add(Btn("Circle", () => ExecTool("circle"), "cad3d.tool.circle", "C"));
        row.Children.Add(Btn("Rect", () => ExecTool("rect"), "cad3d.tool.rect", "R"));
        row.Children.Add(Btn("Wall", () => ExecTool("wall"), "cad3d.tool.wall", "W"));
        row.Children.Add(Btn("Dim", () => ExecTool("dimension"), "cad3d.tool.dimension"));
        row.Children.Add(Btn("Box", () => ExecPrompt("Box(1,1,1)"), "cad3d.tool.box"));
        row.Children.Add(Btn("Extrude", () => ExecPrompt("Extrude(2.4)"), "cad3d.tool.extrude"));
        row.Children.Add(Sep());

        row.Children.Add(SectionLabel("Snap"));
        _snapCheck = new CheckBox
        {
            Content = "Snap to grid",
            IsChecked = _settings.Settings.SnapToGrid,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 8, 0),
        };
        AgentProperties.SetId(_snapCheck, "cad3d.snap", AgentRoleNames.CheckBox);
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
        AgentProperties.SetId(_gridCombo, "cad3d.grid", AgentRoleNames.ComboBox);
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

        row.Children.Add(SectionLabel("Axis lock"));
        _lockNone = AxisLockBtn("Free", "none", "cad3d.axis.none");
        _lockX = AxisLockBtn("X", "x", "cad3d.axis.x");
        _lockY = AxisLockBtn("Y", "y", "cad3d.axis.y");
        _lockZ = AxisLockBtn("Z", "z", "cad3d.axis.z");
        row.Children.Add(_lockNone);
        row.Children.Add(_lockX);
        row.Children.Add(_lockY);
        row.Children.Add(_lockZ);

        SyncDraftOptionsUi();
        return new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(22, 30, 38)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(40, 55, 70)),
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
        if (_snapCheck is null || _gridCombo is null)
            return;

        _syncingDraftUi = true;
        try
        {
            _snapCheck.IsChecked = _settings.Settings.SnapToGrid;
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
        b.Background = on
            ? new SolidColorBrush(Color.FromRgb(40, 90, 110))
            : new SolidColorBrush(Color.FromRgb(28, 38, 48));
        b.Foreground = Brushes.WhiteSmoke;
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

    private void SetStudioWorkspace(StudioWorkspace workspace)
    {
        _workspace = workspace;
        var sceneMode = IsSceneWorkspace(workspace);
        _cadHost.IsVisible = !sceneMode;
        _sceneHost.IsVisible = sceneMode;

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

        RefreshTitle();
        _feedback.SetStatus($"{StudioWorkspaceIds.ToDisplay(workspace)}  ·  active={(sceneMode ? "Scene (.nov3djson)" : "Cad (.cadjson)")}");
    }

    private static TextBlock SectionLabel(string text) => new()
    {
        Text = text,
        FontSize = 11,
        FontWeight = FontWeight.SemiBold,
        Opacity = 0.65,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(4, 0, 6, 0),
        Foreground = new SolidColorBrush(Color.FromRgb(160, 185, 200)),
    };

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
            Background = new SolidColorBrush(Color.FromRgb(22, 32, 42)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(40, 60, 75)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = surface.CreateChrome(Path.Combine(_settings.DataRoot, "dumps")),
            [DockPanel.DockProperty] = Dock.Top,
        };

        return new DockPanel
        {
            Background = new SolidColorBrush(Color.FromRgb(14, 20, 28)),
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
        _commandBar.FocusInput();
    }

    private void OnBridge()
    {
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
        _cad.Execute(new CadCommandDto { ActionId = CadSessionActionIds.New });
        _bridgeDirty = true;
        SetStudioWorkspace(StudioWorkspace.Draft2D);
        _feedback.Flash("New Cad document");
        await Task.CompletedTask;
    }

    private async Task OnOpenAsync()
    {
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

    private void RefreshTitle()
    {
        var dirty = _doc.IsDirty ? " *" : "";
        var active = IsSceneWorkspace(_workspace)
            ? (_scene.DocumentPath is { } p ? Path.GetFileName(p) : _scene.Document.Name + " (bridged)")
            : Path.GetFileName(_doc.DocumentPath);
        Title = $"Novolis CAD Studio 3D — {StudioWorkspaceIds.ToDisplay(_workspace)} — {active}{dirty}";
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

        if (e.Key == Key.S && e.KeyModifiers.HasFlag(KeyModifiers.Control))
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
            if (IsSceneWorkspace(_workspace))
                _scene.Execute(new AgentCommand { ActionId = SceneSessionActionIds.Fit });
            else
                _cad.Execute(new CadCommandDto { ActionId = CadSessionActionIds.Fit });
            e.Handled = true;
        }
        else if (e.Key == Key.X && !e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _cad.Execute(new CadCommandDto { ActionId = CadSessionActionIds.SetAxisLock, Kind = "x" });
            SyncDraftOptionsUi();
            InvalidateDraftViews();
            e.Handled = true;
        }
        else if (e.Key == Key.Y && !e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _cad.Execute(new CadCommandDto { ActionId = CadSessionActionIds.SetAxisLock, Kind = "y" });
            SyncDraftOptionsUi();
            InvalidateDraftViews();
            e.Handled = true;
        }
        else if (e.Key == Key.Z && !e.KeyModifiers.HasFlag(KeyModifiers.Control)
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