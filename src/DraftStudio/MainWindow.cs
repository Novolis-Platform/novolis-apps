using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using DraftStudio.Commands;
using DraftStudio.Core;
using DraftStudio.Models;
using DraftStudio.Services;
using DraftStudio.Ui;
using Novolis.Avalonia.Agent;
using Novolis.Avalonia.Agent.Protocol;
using Novolis.Avalonia.Raylib;
using Novolis.Avalonia.Studio;

namespace DraftStudio;

internal sealed class MainWindow : Window
{
    private readonly DraftSession _session;
    private readonly DraftSettingsStore _settings;
    private readonly DraftCommandBus _bus;
    private readonly DraftCommandDispatcher _dispatcher;
    private readonly ToolController _tools;
    private readonly DraftModelRenderer _modelRenderer;

    private DraftViewport _draftViewport = null!;
    private RaylibHostControl _raylibHost = null!;
    private Panel _viewportStack = null!;
    private StudioCommandBar _commandBar = null!;
    private StudioFeedback _feedback = null!;
    private ListBox _entityList = null!;
    private TextBlock _inspector = null!;
    private DraftViewMode _viewMode = DraftViewMode.Draft;
    private bool _orbiting;
    private Point _lastPointer;
    private bool _suppressList;

    public MainWindow(DraftSession session, DraftSettingsStore settings, DraftCommandBus bus)
    {
        _session = session;
        _settings = settings;
        _bus = bus;
        _dispatcher = new DraftCommandDispatcher(session, bus);
        _tools = new ToolController(_dispatcher);
        _modelRenderer = new DraftModelRenderer(session);

        Title = "Draft Studio";
        Width = 1400;
        Height = 900;

        Content = BuildLayout();

        _dispatcher.FitRequested += OnFit;
        _dispatcher.ToolChanged += UpdateCommandPrompt;
        _tools.Changed += UpdateCommandPrompt;
        _session.Changed += RefreshUi;
        _bus.Changed += RefreshUi;

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

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(8),
        };
        AgentProperties.SetId(toolbar, "draft.toolbar");
        toolbar.Children.Add(Btn("Save", OnSave, "draft.tool.save", "Ctrl+S"));
        toolbar.Children.Add(Sep());
        toolbar.Children.Add(Btn("Select", () => _dispatcher.EnterTool(DraftToolKind.Select), "draft.tool.select"));
        toolbar.Children.Add(Btn("Line", () => _dispatcher.EnterTool(DraftToolKind.Line), "draft.tool.line", "L"));
        toolbar.Children.Add(Btn("Circle", () => _dispatcher.EnterTool(DraftToolKind.Circle), "draft.tool.circle", "C"));
        toolbar.Children.Add(Btn("Rect", () => _dispatcher.EnterTool(DraftToolKind.Rect), "draft.tool.rect", "R"));
        toolbar.Children.Add(Btn("Spline", () => _dispatcher.EnterTool(DraftToolKind.Spline), "draft.tool.spline", "P"));
        toolbar.Children.Add(Sep());
        toolbar.Children.Add(Btn("Box", () => Run("Box(1,1,1)"), "draft.tool.box"));
        toolbar.Children.Add(Btn("Delete", () => Run("Delete"), "draft.tool.delete", "Del"));
        toolbar.Children.Add(Btn("Undo", () => _bus.Undo(), "draft.undo", "Ctrl+Z"));
        toolbar.Children.Add(Btn("Redo", () => _bus.Redo(), "draft.redo", "Ctrl+Y"));
        toolbar.Children.Add(Sep());
        toolbar.Children.Add(Btn("Fit", OnFit, "draft.fit", "F"));
        toolbar.Children.Add(Btn("Draft", () => SetViewMode(DraftViewMode.Draft), "draft.view.draft"));
        toolbar.Children.Add(Btn("Model", () => SetViewMode(DraftViewMode.Model), "draft.view.model"));
        toolbar.Children.Add(Sep());
        toolbar.Children.Add(Btn("Export Phys", OnExportPhys, "draft.export.phys"));

        _draftViewport = new DraftViewport(_session, _settings, _dispatcher, _tools);
        AgentProperties.SetId(_draftViewport, "draft.viewport");
        _raylibHost = new RaylibHostControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsVisible = false,
        };
        AgentProperties.SetId(_raylibHost, "draft.viewport.model");
        _modelRenderer.Bind(_raylibHost);
        _raylibHost.PointerPressed += OnModelPointerPressed;
        _raylibHost.PointerMoved += OnModelPointerMoved;
        _raylibHost.PointerReleased += OnModelPointerReleased;
        _raylibHost.PointerWheelChanged += OnModelWheel;

        _viewportStack = new Panel();
        _viewportStack.Children.Add(_draftViewport);
        _viewportStack.Children.Add(_raylibHost);

        _commandBar = new StudioCommandBar();
        AgentProperties.SetId(_commandBar, "draft.commandBar");
        _commandBar.Submitted += (_, e) =>
        {
            var err = _dispatcher.TryDispatch(e.Text);
            if (err is not null)
                _feedback.FlashError(err);
            else
                _feedback.SetStatus($"OK — {e.Text}");
            UpdateCommandPrompt();
        };
        _commandBar.Cancelled += (_, _) =>
        {
            _tools.Cancel();
            UpdateCommandPrompt();
        };

        var center = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        center.Children.Add(toolbar);
        Grid.SetRow(_viewportStack, 1);
        center.Children.Add(_viewportStack);
        Grid.SetRow(_commandBar, 2);
        center.Children.Add(_commandBar);

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
        _session.OpenOrCreateDefault();
        if (string.Equals(_settings.Settings.ViewMode, "model", StringComparison.OrdinalIgnoreCase))
            SetViewMode(DraftViewMode.Model);
        else
            SetViewMode(DraftViewMode.Draft);
        OnFit();
        UpdateCommandPrompt();
        _feedback.SetStatus($"Workspace: {_session.WorkspacePath} ({Path.GetFileName(_session.DocumentPath)})");
        _commandBar.FocusInput();
    }

    private void RefreshUi()
    {
        _suppressList = true;
        _entityList.ItemsSource = _session.Document.Entities.ToList();
        var selected = _session.SelectedEntity;
        _entityList.SelectedItem = selected;
        _suppressList = false;

        _inspector.Text = selected is null
            ? "No selection.\n\nCommands:\n  Line(x1,z1,x2,z2)\n  Circle(cx,cz,r)\n  Rect(x1,z1,x2,z2)\n  Spline(x1,z1,...)\n  Box(w,h,d)\n  Move(dx,dy,dz)\n  Undo / Redo / Delete / Fit\n\nSaves as draft.cadjson"
            : $"{selected.Summary}\nKind: {selected.Kind}\nId: {selected.Id:N}";

        Title = _session.IsDirty ? "Draft Studio *" : "Draft Studio";
        _draftViewport.InvalidateVisual();
        if (_viewMode == DraftViewMode.Model)
            _raylibHost.RequestFrame();
    }

    private void UpdateCommandPrompt() =>
        _commandBar.PromptLabel = _tools.PromptHint;

    private void SetViewMode(DraftViewMode mode)
    {
        _viewMode = mode;
        _settings.Settings.ViewMode = mode == DraftViewMode.Model ? "model" : "draft";
        _draftViewport.IsVisible = mode == DraftViewMode.Draft;
        _raylibHost.IsVisible = mode == DraftViewMode.Model;
        if (mode == DraftViewMode.Model)
        {
            _raylibHost.FrameWidth = Math.Max(64, (int)_viewportStack.Bounds.Width);
            _raylibHost.FrameHeight = Math.Max(64, (int)_viewportStack.Bounds.Height);
            _raylibHost.SetHostActive(true);
            _raylibHost.RequestFrame();
        }

        _feedback.SetStatus(mode == DraftViewMode.Model ? "Model view" : "Draft view (XZ plan)");
    }

    private void OnFit()
    {
        if (_viewMode == DraftViewMode.Draft)
            _draftViewport.Fit();
        else
        {
            _modelRenderer.Fit();
            _raylibHost.RequestFrame();
        }
    }

    private void OnSave()
    {
        _session.Save();
        _feedback.Flash("Saved draft.cadjson");
    }

    private void OnExportPhys()
    {
        var exporter = new CadPhysExporter();
        var phys = exporter.Build(_session.Document);
        exporter.Write(phys, _settings.PhysDocumentPath);
        _feedback.Flash($"Exported {Path.GetFileName(_settings.PhysDocumentPath)} ({phys.Meshes.Count} meshes)");
    }

    private void Run(string prompt)
    {
        var err = _dispatcher.TryDispatch(prompt);
        if (err is not null)
            _feedback.FlashError(err);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
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
        var b = new Button { Content = text, Padding = new Thickness(10, 4) };
        AgentProperties.SetId(b, agentId, AgentRoleNames.Button);
        if (tip is not null)
            ToolTip.SetTip(b, tip);
        b.Click += (_, _) => action();
        return b;
    }

    private static Control Sep() => new Border { Width = 8 };
}
