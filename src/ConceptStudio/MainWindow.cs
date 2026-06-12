using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ConceptStudio.Core;
using ConceptStudio.Models;
using ConceptStudio.Services;
using ConceptStudio.Ui;
using Novolis.Avalonia.Raylib;
using Novolis.Avalonia.Rendering;
using Novolis.Avalonia.Studio;

namespace ConceptStudio;

internal sealed class MainWindow : Window
{
    private const int QualitySampleTarget = 192;
    private const int QualitySampleThrottle = 64;

    private readonly ConceptSession _session;
    private readonly ConceptSceneStore _scenes;
    private readonly ConceptSettingsStore _settings;
    private readonly PathTraceViewport _viewport;
    private readonly ViewportModeCoordinator _coordinator;

    private readonly RaylibHostControl _raylibHost = new()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
    };
    private readonly Rgba32FrameControl _frame = new()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
    };
    private readonly DimensionOverlay _dimensionOverlay = new()
    {
        IsHitTestVisible = false,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
    };
    private Grid _viewportHost = null!;
    private readonly ListBox _partsList = new();
    private readonly PartInspectorPanel _inspector = new();
    private readonly TextBlock _viewportHint = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        TextAlignment = TextAlignment.Center,
        FontSize = 14,
        Opacity = 0.85,
        IsHitTestVisible = false,
    };

    private readonly Button _previewModeButton;
    private readonly Button _qualityModeButton;
    private readonly ComboBox _viewModeCombo = new() { MinWidth = 120, Margin = new Thickness(4, 0, 0, 0) };

    private readonly TextBlock _workspacePath = new() { Opacity = 0.7, FontSize = 11, Margin = new Thickness(8) };

    private StudioFeedback _feedback = null!;
    private ConceptPartRecord? _selectedPart;
    private ConceptViewMode _viewMode = ConceptViewMode.Orbit;
    private bool _orbiting;
    private bool _movingPart;
    private int _qualityRebuildInFlight;
    private int _boxCounter;
    private int _cylinderCounter;
    private int _coneCounter;
    private int _sphereCounter;
    private int _wedgeCounter;
    private Point _lastPointer;
    private DispatcherTimer? _renderTimer;
    private DispatcherTimer? _editIdleTimer;
    private bool _inspectorEditActive;
    private CancellationTokenSource? _rebuildCts;
    private Size _lastViewportSize;
    private int _qualityTick;
    private int _uiTick;
    private string _lastStatusText = string.Empty;

    public MainWindow(
        ConceptSession session,
        ConceptSceneStore scenes,
        ConceptSettingsStore settings,
        PathTraceViewport viewport)
    {
        _session = session;
        _scenes = scenes;
        _settings = settings;
        _viewport = viewport;
        _coordinator = new ViewportModeCoordinator(viewport, _raylibHost);

        Title = "Concept Studio";
        Width = 1480;
        Height = 920;

        _previewModeButton = Button("Preview", OnPreviewMode);
        _qualityModeButton = Button("Quality", OnQualityMode);
        _viewModeCombo.ItemsSource = new[] { "Orbit", "Plan", "Profile", "Bow" };
        _viewModeCombo.SelectedIndex = 0;
        _viewModeCombo.SelectionChanged += OnViewModeChanged;

        Content = BuildLayout();

        _coordinator.BindScene(
            () => _session.Document,
            () => _viewMode,
            () => _settings.Settings.ShowWireframe,
            () => _viewMode != ConceptViewMode.Orbit || true);
        _coordinator.ModeChanged += OnViewportModeChanged;
        _coordinator.QualityRebuildDue += OnQualityRebuildDue;

        Opened += OnOpened;
        Closing += (_, _) => _settings.Save();
        KeyDown += OnKeyDown;
    }

    private Control BuildLayout()
    {
        var chrome = StudioChrome.Create();
        _feedback = chrome.CreateFeedback();

        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(8) };
        toolbar.Children.Add(Button("Save", OnSave, "Ctrl+S"));
        toolbar.Children.Add(Separator());
        toolbar.Children.Add(Button("Box", OnAddBox, "B"));
        toolbar.Children.Add(Button("Cylinder", OnAddCylinder, "C"));
        toolbar.Children.Add(Button("Cone", OnAddCone, "N"));
        toolbar.Children.Add(Button("Sphere", OnAddSphere, "S"));
        toolbar.Children.Add(Button("Wedge", OnAddWedge, "W"));
        toolbar.Children.Add(Button("Group", OnAddGroup));
        toolbar.Children.Add(Separator());
        toolbar.Children.Add(Button("Duplicate", OnDuplicate, "Ctrl+D"));
        toolbar.Children.Add(Button("Delete", OnDeletePart, "Del"));
        toolbar.Children.Add(Separator());
        toolbar.Children.Add(Button("Dimension", OnAddDimension));
        toolbar.Children.Add(Separator());
        toolbar.Children.Add(Button("Fit", OnFitView, "F"));
        toolbar.Children.Add(_viewModeCombo);
        toolbar.Children.Add(Separator());
        toolbar.Children.Add(Button("Export PNG", OnExportPng));
        toolbar.Children.Add(Button("Export SVG", OnExportSvg));
        toolbar.Children.Add(Separator());
        toolbar.Children.Add(_previewModeButton);
        toolbar.Children.Add(_qualityModeButton);

        _viewportHost = new Grid();
        _viewportHost.Children.Add(_raylibHost);
        _viewportHost.Children.Add(_frame);
        _viewportHost.Children.Add(_dimensionOverlay);
        _viewportHost.Children.Add(_viewportHint);
        _viewportHost.Children.Add(chrome.BusyOverlay);
        _frame.IsVisible = false;

        _viewportHost.PointerPressed += OnViewportPointerPressed;
        _viewportHost.PointerReleased += OnViewportPointerReleased;
        _viewportHost.PointerMoved += OnViewportPointerMoved;
        _viewportHost.PointerWheelChanged += OnViewportWheel;
        _viewportHost.LayoutUpdated += (_, _) => EnsureViewportSized();

        _partsList.ItemTemplate = new FuncDataTemplate<ConceptPartRecord>((part, _) =>
            new TextBlock { Text = part?.Summary ?? string.Empty },
            supportsRecycling: true);

        _partsList.SelectionChanged += (_, _) =>
        {
            _selectedPart = _partsList.SelectedItem as ConceptPartRecord;
            _inspector.Bind(_selectedPart);
            _feedback.Flash(_selectedPart is null ? "Selection cleared" : $"Selected {_selectedPart.Name}");
        };
        _inspector.PartChanged += (_, _) => OnInspectorEdited();

        var viewportPanel = new DockPanel();
        DockPanel.SetDock(chrome.FlashLine, Dock.Bottom);
        DockPanel.SetDock(chrome.StatusLine, Dock.Bottom);
        viewportPanel.Children.Add(chrome.FlashLine);
        viewportPanel.Children.Add(chrome.StatusLine);
        viewportPanel.Children.Add(_viewportHost);

        var center = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        center.Children.Add(toolbar);
        Grid.SetRow(viewportPanel, 1);
        center.Children.Add(viewportPanel);

        var left = new DockPanel();
        var leftHeader = new TextBlock { Text = "Parts", FontWeight = FontWeight.SemiBold, Margin = new Thickness(8, 8, 8, 4) };
        DockPanel.SetDock(leftHeader, Dock.Top);
        DockPanel.SetDock(_workspacePath, Dock.Bottom);
        left.Children.Add(leftHeader);
        left.Children.Add(_workspacePath);
        left.Children.Add(_partsList);

        var right = new DockPanel();
        var rightHeader = new TextBlock { Text = "Inspector", FontWeight = FontWeight.SemiBold, Margin = new Thickness(8, 8, 8, 4) };
        DockPanel.SetDock(rightHeader, Dock.Top);
        right.Children.Add(rightHeader);
        right.Children.Add(_inspector);

        return new ConceptWorkspace(_settings, left, center, right);
    }

    private static Control Separator() => new Border { Width = 8 };

    private static Button Button(string text, EventHandler<RoutedEventArgs> click, string? tooltip = null)
    {
        var button = new Button { Content = text };
        if (tooltip is not null)
            ToolTip.SetTip(button, tooltip);
        button.Click += click;
        return button;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        _feedback.Flash("Loading workspace…");
        try
        {
            _viewport.Attach(_frame);
            _session.OpenOrCreateDefault();
            _workspacePath.Text = _session.WorkspacePath;
            CountParts();
            SyncCameraFromScene();
            _coordinator.StartInFastMode();
            OnViewportModeChanged(_coordinator.Mode);
            StartRenderLoop();
            Dispatcher.UIThread.Post(EnsureViewportSized, DispatcherPriority.Loaded);
            RefreshUi();
            _feedback.Flash($"Ready — {_session.Document.Parts.Count} parts");
        }
        catch (Exception ex)
        {
            _feedback.FlashError($"Open failed: {ex.Message}");
        }
    }

    private void CountParts()
    {
        _boxCounter = _session.Document.Parts.Count(p => p.Kind.Equals("box", StringComparison.OrdinalIgnoreCase));
        _cylinderCounter = _session.Document.Parts.Count(p => p.Kind.Equals("cylinder", StringComparison.OrdinalIgnoreCase));
        _coneCounter = _session.Document.Parts.Count(p => p.Kind.Equals("cone", StringComparison.OrdinalIgnoreCase));
        _sphereCounter = _session.Document.Parts.Count(p => p.Kind.Equals("sphere", StringComparison.OrdinalIgnoreCase));
        _wedgeCounter = _session.Document.Parts.Count(p => p.Kind.Equals("wedge", StringComparison.OrdinalIgnoreCase));
    }

    private void StartRenderLoop()
    {
        _renderTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Background, (_, _) =>
        {
            EnsureViewportSized();
            if (ShouldRunQualityPass())
            {
                _qualityTick++;
                if ((_qualityTick & 1) == 0)
                {
                    var samples = _viewport.DisplayedSamples;
                    var batch = samples >= QualitySampleTarget ? 2 : samples >= QualitySampleThrottle ? 4 : 8;
                    _coordinator.TickPathTrace(batch);
                }
            }

            if ((_uiTick++ & 7) == 0)
            {
                UpdateViewportHint();
                UpdateStatus();
                _dimensionOverlay.Update(_session.Document, _viewMode);
            }
        });
        _renderTimer.Start();
    }

    private bool ShouldRunQualityPass() =>
        _coordinator.QualityPinned
        && _coordinator.Mode == ViewportDisplayMode.QualityRefine
        && !_coordinator.IsInteracting
        && !_movingPart
        && !_orbiting
        && !_inspectorEditActive;

    private void EnsureViewportSized()
    {
        var size = _viewportHost.Bounds.Size;
        if (size.Width < 1 || size.Height < 1)
            return;

        if (Math.Abs(size.Width - _lastViewportSize.Width) < 1 && Math.Abs(size.Height - _lastViewportSize.Height) < 1)
            return;

        _lastViewportSize = size;
        _raylibHost.FrameWidth = (int)size.Width;
        _raylibHost.FrameHeight = (int)size.Height;
        if (_coordinator.Mode == ViewportDisplayMode.FastPreview)
        {
            _raylibHost.EnsureHostStarted();
            _raylibHost.RequestFrame();
        }

        if (_coordinator.Mode == ViewportDisplayMode.QualityRefine)
            _coordinator.TryResizePathTrace(size.Width, size.Height);
    }

    private void OnViewportModeChanged(ViewportDisplayMode mode)
    {
        var fast = mode == ViewportDisplayMode.FastPreview;
        _raylibHost.IsVisible = fast;
        _frame.IsVisible = !fast;
        _dimensionOverlay.IsVisible = fast && _viewMode != ConceptViewMode.Orbit;
        _viewport.SetRenderScale(fast ? 1f : _orbiting || _movingPart ? 0.5f : 1f);
        _lastViewportSize = default;
        UpdateModeButtons();
        Dispatcher.UIThread.Post(() =>
        {
            EnsureViewportSized();
            if (!fast)
                _ = RebuildQualityViewportAsync();
        }, DispatcherPriority.Loaded);
    }

    private void UpdateModeButtons()
    {
        var preview = _coordinator.Mode == ViewportDisplayMode.FastPreview;
        _previewModeButton.FontWeight = preview ? FontWeight.Bold : FontWeight.Normal;
        _qualityModeButton.FontWeight = preview ? FontWeight.Normal : FontWeight.Bold;
        _previewModeButton.Content = preview ? "● Preview" : "Preview";
        _qualityModeButton.Content = preview ? "Quality" : "● Quality";
    }

    private void OnPreviewMode(object? sender, RoutedEventArgs e)
    {
        _coordinator.SetQualityPinned(false);
        _feedback.Flash("Preview mode");
        UpdateModeButtons();
    }

    private void OnQualityMode(object? sender, RoutedEventArgs e)
    {
        _coordinator.SetQualityPinned(true);
        _feedback.Flash("Quality path trace");
        UpdateModeButtons();
    }

    private void OnViewModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        _viewMode = _viewModeCombo.SelectedIndex switch
        {
            1 => ConceptViewMode.Plan,
            2 => ConceptViewMode.Profile,
            3 => ConceptViewMode.Bow,
            _ => ConceptViewMode.Orbit,
        };
        _dimensionOverlay.IsVisible = _viewMode != ConceptViewMode.Orbit && _coordinator.Mode == ViewportDisplayMode.FastPreview;
        _dimensionOverlay.Update(_session.Document, _viewMode);
        _coordinator.NotifySceneChanged();
    }

    private void UpdateViewportHint()
    {
        if (_coordinator.Mode == ViewportDisplayMode.FastPreview)
        {
            _viewportHint.IsVisible = false;
            return;
        }

        _viewportHint.IsVisible = !(_viewport.IsReady && _viewport.LastFramePresented);
        _viewportHint.Text = _viewport.Status;
    }

    private void UpdateStatus()
    {
        var dirty = _session.IsDirty ? "  ● unsaved" : string.Empty;
        var partCount = _session.Document.Parts.Count(p => !p.IsGroup);
        var mode = _coordinator.Mode == ViewportDisplayMode.FastPreview ? "Preview" : "Quality";
        var view = _viewMode.ToString();
        var text =
            $"{mode}  |  {view}  |  {partCount} part{(partCount == 1 ? string.Empty : "s")}{dirty}  |  {_viewport.Status}  |  Shift+drag move  |  Ctrl+S save";
        if (text == _lastStatusText)
            return;
        _lastStatusText = text;
        _feedback.SetStatus(text);
    }

    private void RefreshUi()
    {
        _partsList.ItemsSource = _session.Document.Parts.ToList();
        if (_selectedPart is not null && !_session.Document.Parts.Contains(_selectedPart))
            _selectedPart = null;
        _inspector.Bind(_selectedPart);
        _dimensionOverlay.Update(_session.Document, _viewMode);
        UpdateStatus();
    }

    private void OnInspectorEdited()
    {
        _inspectorEditActive = true;
        _coordinator.NotifyInteractionStarted();
        OnSceneEdited(bumpGeometry: true);
        _editIdleTimer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, (_, _) =>
        {
            _editIdleTimer?.Stop();
            _inspectorEditActive = false;
            _coordinator.NotifyInteractionEnded();
        });
        _editIdleTimer.Stop();
        _editIdleTimer.Start();
    }

    private void OnSceneEdited(bool bumpGeometry = true)
    {
        _session.MarkDirty();
        if (bumpGeometry)
        {
            _session.BumpSceneGeometry();
            _scenes.InvalidateCompileCache();
        }

        _coordinator.NotifySceneChanged(immediateQualityRebuild: false);
        RefreshUi();
    }

    private void OnQualityRebuildDue() => _ = RebuildQualityViewportAsync();

    private async Task RebuildQualityViewportAsync()
    {
        if (!_coordinator.QualityPinned || _coordinator.Mode != ViewportDisplayMode.QualityRefine || _coordinator.IsInteracting)
            return;

        _rebuildCts?.Cancel();
        _rebuildCts = new CancellationTokenSource();
        var ct = _rebuildCts.Token;

        if (Interlocked.CompareExchange(ref _qualityRebuildInFlight, 1, 0) != 0)
            return;

        try
        {
            _session.Document.Camera = _coordinator.CaptureCameraState();
            SyncPathTraceCamera();
            var revision = _session.SceneRevision;
            var document = _session.Document;
            var compiled = await Task.Run(() => _scenes.Compile(document, revision), ct).ConfigureAwait(true);
            if (ct.IsCancellationRequested || revision != _session.SceneRevision)
                return;

            _viewport.SetScene(compiled);
            _viewport.BeginTracing();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            Interlocked.Exchange(ref _qualityRebuildInFlight, 0);
        }
    }

    private void SyncCameraFromScene()
    {
        _coordinator.ApplyCameraFromScene(_session.Document.Camera);
        SyncPathTraceCamera();
        EnsureViewportSized();
    }

    private void SyncPathTraceCamera() =>
        _viewport.ApplyCameraState(_coordinator.CaptureCameraState());

    private void OnFitView(object? sender, RoutedEventArgs e)
    {
        var (center, radius) = SceneBounds.Compute(_session.Document);
        _coordinator.Orbit.Target = center;
        _coordinator.Orbit.Distance = MathF.Max(4f, radius * 2.8f);
        _session.Document.Camera = _coordinator.CaptureCameraState();
        SyncPathTraceCamera();
        if (_coordinator.Mode == ViewportDisplayMode.QualityRefine)
            _viewport.ResetAccumulation();
        OnSceneEdited(bumpGeometry: false);
        _feedback.Flash("Fit view");
    }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        _session.Document.Camera = _coordinator.CaptureCameraState();
        _session.Save();
        _feedback.Flash("Saved concept.json");
        UpdateStatus();
    }

    private void OnAddBox(object? sender, RoutedEventArgs e) => AddPart("box");

    private void OnAddCylinder(object? sender, RoutedEventArgs e) => AddPart("cylinder");

    private void OnAddCone(object? sender, RoutedEventArgs e) => AddPart("cone");

    private void OnAddSphere(object? sender, RoutedEventArgs e) => AddPart("sphere");

    private void OnAddWedge(object? sender, RoutedEventArgs e) => AddPart("wedge");

    private void AddPart(string kind)
    {
        var part = CreatePart(kind);
        _session.Document.Parts.Add(part);
        _selectedPart = part;
        _coordinator.NotifyInteractionStarted();
        OnSceneEdited();
        _partsList.SelectedItem = part;
        _coordinator.NotifyInteractionEnded();
        _feedback.Flash($"Added {part.Name}");
    }

    private ConceptPartRecord CreatePart(string kind)
    {
        return kind switch
        {
            "cylinder" => new ConceptPartRecord
            {
                Name = $"Cylinder {++_cylinderCounter}",
                Kind = "cylinder",
                Center = [Random.Shared.NextSingle() * 2f - 1f, 1f, Random.Shared.NextSingle() * 2f - 1f],
                Radius = 0.5f,
                Height = 2f,
                Material = "metal",
                Color = [0.7f, 0.72f, 0.78f],
            },
            "cone" => new ConceptPartRecord
            {
                Name = $"Cone {++_coneCounter}",
                Kind = "cone",
                Center = [Random.Shared.NextSingle(), 1.2f, Random.Shared.NextSingle()],
                Radius = 0.6f,
                Height = 1.5f,
                Material = "hull",
                Color = [0.5f, 0.52f, 0.55f],
            },
            "sphere" => new ConceptPartRecord
            {
                Name = $"Sphere {++_sphereCounter}",
                Kind = "sphere",
                Center = [Random.Shared.NextSingle() * 2f, 0.8f, Random.Shared.NextSingle() * 2f],
                Radius = 0.45f,
                Material = "engineglow",
                Color = [1f, 0.55f, 0.2f],
            },
            "wedge" => new ConceptPartRecord
            {
                Name = $"Wedge {++_wedgeCounter}",
                Kind = "wedge",
                Center = [0f, 1f, 0f],
                HalfExtents = [1f, 0.6f, 1.5f],
                Material = "hulldark",
                Color = [0.35f, 0.38f, 0.42f],
            },
            _ => new ConceptPartRecord
            {
                Name = $"Box {++_boxCounter}",
                Kind = "box",
                Center = [Random.Shared.NextSingle() * 0.8f - 0.4f, 0.5f, Random.Shared.NextSingle() * 0.8f - 0.4f],
                HalfExtents = [0.35f, 0.35f, 0.35f],
                Material = "hull",
                Color = [0.72f, 0.35f, 0.28f],
            },
        };
    }

    private void OnAddGroup(object? sender, RoutedEventArgs e)
    {
        var group = new ConceptPartRecord { Name = "Group", Kind = "group" };
        _session.Document.Parts.Insert(0, group);
        OnSceneEdited(bumpGeometry: false);
        _feedback.Flash("Added group");
    }

    private void OnDuplicate(object? sender, RoutedEventArgs e)
    {
        if (_selectedPart is null || _selectedPart.IsGroup)
        {
            _feedback.FlashWarning("Select a primitive to duplicate");
            return;
        }

        var copy = _selectedPart.Clone();
        copy.Center[0] += 0.5f;
        _session.Document.Parts.Add(copy);
        _selectedPart = copy;
        OnSceneEdited();
        _partsList.SelectedItem = copy;
        _feedback.Flash($"Duplicated {copy.Name}");
    }

    private void OnDeletePart(object? sender, RoutedEventArgs e)
    {
        if (_selectedPart is null)
        {
            _feedback.FlashWarning("Select a part to delete");
            return;
        }

        _session.Document.Parts.Remove(_selectedPart);
        _selectedPart = null;
        OnSceneEdited();
        _feedback.Flash("Deleted part");
    }

    private void OnAddDimension(object? sender, RoutedEventArgs e)
    {
        var view = _viewMode switch
        {
            ConceptViewMode.Plan => "plan",
            ConceptViewMode.Bow => "bow",
            ConceptViewMode.Profile => "profile",
            _ => "profile",
        };

        var (center, radius) = SceneBounds.Compute(_session.Document);
        _session.Document.Annotations.Add(new AnnotationRecord
        {
            View = view,
            From = [center.X - radius * 0.5f, center.Y, center.Z],
            To = [center.X + radius * 0.5f, center.Y, center.Z],
        });
        OnSceneEdited(bumpGeometry: false);
        _dimensionOverlay.Update(_session.Document, _viewMode);
        _feedback.Flash($"Added dimension on {view} view");
    }

    private async void OnExportSvg(object? sender, RoutedEventArgs e)
    {
        var file = await PickSaveFileAsync("concept-sheet.svg", ["SVG files", "*.svg"]);
        if (file is null)
            return;

        try
        {
            await _feedback.RunAsync("Exporting SVG…", "SVG exported", async () =>
            {
                var svg = ConceptSheetExporter.ExportSvg(_session.Document, _session.Document.Name);
                await File.WriteAllTextAsync(file, svg);
            });
        }
        catch
        {
        }
    }

    private async void OnExportPng(object? sender, RoutedEventArgs e)
    {
        var file = await PickSaveFileAsync("concept-perspective.png", ["PNG files", "*.png"]);
        if (file is null)
            return;

        try
        {
            await _feedback.RunAsync("Rendering PNG…", "PNG exported", async () =>
            {
                _session.Document.Camera = _coordinator.CaptureCameraState();
                await Task.Run(() => ConceptQualityExporter.ExportPerspectivePng(
                    _scenes,
                    _session.Document,
                    _session.SceneRevision,
                    _session.Document.Camera,
                    file));
            });
        }
        catch (Exception ex)
        {
            _feedback.FlashError($"Export failed: {ex.Message}");
        }
    }

    private async Task<string?> PickSaveFileAsync(string defaultName, string[] filter)
    {
        var storage = StorageProvider;
        if (storage is null)
            return null;

        var result = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export",
            SuggestedFileName = defaultName,
            FileTypeChoices =
            [
                new FilePickerFileType(filter[0]) { Patterns = [filter[1]] },
            ],
        });
        return result?.TryGetLocalPath();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.S && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            OnSave(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        if (e.Key == Key.D && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            OnDuplicate(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete)
        {
            OnDeletePart(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F)
        {
            OnFitView(this, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private void OnViewportPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _lastPointer = e.GetPosition(_viewportHost);
        if (e.GetCurrentPoint(_viewportHost).Properties.IsLeftButtonPressed)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && _selectedPart is not null && !_selectedPart.IsGroup)
                _movingPart = true;
            else if (_viewMode == ConceptViewMode.Orbit)
                _orbiting = true;

            _coordinator.NotifyInteractionStarted();
        }
    }

    private void OnViewportPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_movingPart || _orbiting)
        {
            _movingPart = false;
            _orbiting = false;
            _coordinator.NotifyInteractionEnded();
        }
    }

    private void OnViewportPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_orbiting && !_movingPart)
            return;

        var pos = e.GetPosition(_viewportHost);
        var dx = (float)(pos.X - _lastPointer.X);
        var dy = (float)(pos.Y - _lastPointer.Y);
        _lastPointer = pos;

        if (_movingPart && _selectedPart is not null)
        {
            var step = _settings.Settings.GridStep;
            var snap = _settings.Settings.SnapToGrid && !e.KeyModifiers.HasFlag(KeyModifiers.Shift);
            var scale = 0.02f * _coordinator.Orbit.Distance;
            _selectedPart.Center[0] = GridSnap.Snap(_selectedPart.Center[0] + dx * scale, step, snap);
            _selectedPart.Center[2] = GridSnap.Snap(_selectedPart.Center[2] - dy * scale, step, snap);
            OnSceneEdited();
            _inspector.Bind(_selectedPart);
            return;
        }

        if (_viewMode == ConceptViewMode.Orbit)
        {
            _coordinator.Orbit.Yaw += dx * 0.008f;
            _coordinator.Orbit.Pitch = Math.Clamp(_coordinator.Orbit.Pitch + dy * 0.008f, -1.45f, 1.45f);
            _coordinator.NotifySceneChanged();
        }
    }

    private void OnViewportWheel(object? sender, PointerWheelEventArgs e)
    {
        if (_viewMode != ConceptViewMode.Orbit)
            return;

        _coordinator.Orbit.Distance = Math.Clamp(_coordinator.Orbit.Distance - (float)e.Delta.Y * 0.8f, 2f, 200f);
        _coordinator.NotifySceneChanged();
    }
}
