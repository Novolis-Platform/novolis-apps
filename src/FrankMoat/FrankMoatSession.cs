using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using FrankMoat.Game;
using FrankMoat.Input;
using FrankMoat.Levels;
using FrankMoat.Narrative;
using FrankMoat.Rendering;
using Novolis.Avalonia.Gaming;
using Novolis.Avalonia.GraphicalProfile;
using Novolis.Avalonia.Rendering;
using Novolis.Game.Scenes;
using Novolis.Math.Geometry;
using Novolis.Rendering.Planar;
using PresentationMouseButton = Novolis.Rendering.Presentation.MouseButton;

namespace FrankMoat;

internal sealed class FrankMoatSession : IDisposable
{
    private readonly PlanarSceneControl _viewport;
    private readonly GameShell _shell;
    private readonly FrankMoatHud _hud;
    private readonly PlanarScene _scene = new();
    private readonly FrankMoatWorld _world = new();
    private readonly FrankMoatPresenter _presenter;
    private readonly IReadOnlyList<FloorDefinition> _floors = FacilityCatalog.Build();
    private readonly FrankInputBuffer _input = new();
    private readonly WindowsGamepadReader _gamepad = new();
    private readonly FixedStepClock _clock = new();
    private readonly ElevatorSystem _elevator = new();
    private readonly CampaignSaveStore _saveStore = new();
    private CampaignState _campaign = new();
    private FounderRecording? _recording;
    private string _status = "Morning round in progress.";
    private bool _disposed;

    public FrankMoatSession(
        PlanarSceneControl viewport,
        GameShell shell,
        FrankMoatHud hud)
    {
        _viewport = viewport;
        _shell = shell;
        _hud = hud;
        _presenter = new FrankMoatPresenter(
            FrankMoatArt.Create(_scene.Textures));

        _viewport.Scene = _scene;
        _viewport.FrameUpdating += OnFrameUpdating;
        _viewport.ScenePointerPressed += OnScenePointerPressed;
        _viewport.ScenePointerReleased += OnScenePointerReleased;
        _shell.Modal.DismissRequested += OnModalDismissed;

        _campaign.Reset();
        _world.Reset(_campaign);
        StartFloor(FloorId.Level0, preservePosition: false);
    }

    public void KeyDown(Key key) => _input.KeyDown(key);

    public void KeyUp(Key key) => _input.KeyUp(key);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _viewport.FrameUpdating -= OnFrameUpdating;
        _viewport.ScenePointerPressed -= OnScenePointerPressed;
        _viewport.ScenePointerReleased -= OnScenePointerReleased;
        _shell.Modal.DismissRequested -= OnModalDismissed;
    }

    private void OnFrameUpdating(object? sender, PlanarFrameEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        _scene.Camera.ViewportWidth = Math.Max(1, _viewport.PixelWidth);
        _scene.Camera.ViewportHeight = Math.Max(1, _viewport.PixelHeight);
        var gamepad = _gamepad.Read();
        var frame = _input.BuildFrame(
            _scene,
            _viewport.MousePixelPosition,
            _viewport.PixelWidth,
            _viewport.PixelHeight,
            _world.Frank.Position,
            gamepad);

        if (frame.PausePressed)
        {
            TogglePause();
            _input.ConsumeEdges();
        }

        if (_shell.IsPaused || _shell.IsModalOpen)
        {
            RefreshPresentation();
            return;
        }

        if (frame.SavePressed)
        {
            SaveCampaign();
        }
        else if (frame.LoadPressed)
        {
            LoadCampaign();
            _input.ConsumeEdges();
            RefreshPresentation();
            return;
        }

        if (_recording is not null)
        {
            if (frame.InteractPressed)
            {
                _recording = null;
                _world.Inspecting = false;
                _status = "Recording ended.";
            }

            _input.ConsumeEdges();
            RefreshPresentation();
            return;
        }

        if (frame.MapPressed)
        {
            _world.MapVisible = !_world.MapVisible;
        }

        if (frame.InspectPressed)
        {
            _world.Inspecting = !_world.Inspecting;
        }

        if (frame.InteractPressed)
        {
            HandleInteraction();
        }

        if (_elevator.State != ElevatorState.Idle)
        {
            TickElevator(e.DeltaSeconds);
            _input.ConsumeEdges();
            RefreshPresentation();
            return;
        }

        if (_world.MapVisible)
        {
            _input.ConsumeEdges();
            RefreshPresentation();
            return;
        }

        var steps = _clock.Consume(e.DeltaSeconds);
        if (steps > 0)
        {
            for (var i = 0; i < steps; i++)
            {
                _world.Tick(
                    _clock.StepSeconds,
                    _scene.Collision,
                    i == 0 ? frame : frame.WithoutEdges(),
                    _campaign);
            }

            _input.ConsumeEdges();
        }

        if (_world.Frank.Health <= 0)
        {
            _world.Inspecting = true;
            _status = "Frank is down. Press F9 to restore the last save or F5 to create a new checkpoint.";
        }

        _world.Lighting.SetMode(
            _world.Floor.Id,
            _campaign.DirectiveActive,
            _world.IsCombat,
            elevator: false);
        UpdateCamera(frame.AimWorld, e.DeltaSeconds);
        RefreshPresentation();
    }

    private void OnScenePointerPressed(object? sender, PlanarPointerEventArgs e)
    {
        _input.PointerPressed(e.Button);
    }

    private void OnScenePointerReleased(object? sender, PlanarPointerEventArgs e)
    {
        _input.PointerReleased(e.Button);
    }

    private void HandleInteraction()
    {
        var result = _world.Interact(_campaign);
        if (result is null)
        {
            return;
        }

        _status = result.Message;
        _world.ApplyCollision(_scene);
        if (result.Recording is not null)
        {
            _recording = result.Recording;
            _world.Inspecting = true;
            return;
        }

        if (result.ElevatorDestination is { } destination)
        {
            BeginElevator(destination);
        }
    }

    private void BeginElevator(FloorId destination)
    {
        _world.CaptureTo(_campaign);
        if (!_elevator.TryStart(_campaign, destination))
        {
            _status = "The elevator refuses the request.";
            return;
        }

        _saveStore.Save(_campaign);
        _status = _elevator.StatusText;
    }

    private void TickElevator(float dt)
    {
        _world.Lighting.SetMode(
            _world.Floor.Id,
            _campaign.DirectiveActive,
            combat: false,
            elevator: true);
        _world.Lighting.Tick(dt, false);
        var arrived = _elevator.Tick(dt);
        _status = _elevator.StatusText;
        if (arrived is not { } destination)
        {
            UpdateCamera(Vector3.UnitZ, dt);
            RefreshPresentation();
            return;
        }

        var floor = FacilityCatalog.Find(_floors, destination);
        _campaign.CurrentFloor = destination;
        _campaign.PlayerPosition = floor.StartPosition;
        StartFloor(destination, preservePosition: false);
        _saveStore.Save(_campaign);
    }

    private void StartFloor(FloorId floorId, bool preservePosition)
    {
        var floor = FacilityCatalog.Find(_floors, floorId);
        _campaign.CurrentFloor = floorId;
        if (!preservePosition)
        {
            _campaign.PlayerPosition = floor.StartPosition;
        }

        _world.LoadFloor(floor, _campaign);
        _world.ApplyCollision(_scene);
        _presenter.BuildStatic(_scene, _world);
        _scene.Camera.WorldUnitsPerPixel = 0.018f;
        _scene.Camera.Position = _world.Frank.Position;
        _scene.Camera.ClearColor = _world.Lighting.AmbientColor;
        _clock.Reset();
        _recording = null;
        _world.Inspecting = false;
        _world.MapVisible = false;
        _status = floor.EntryPrompt;
        _world.Faith.Say(floor.EntryPrompt, 4.5f, priority: 70);
        if (floorId == FloorId.Level0 && !_campaign.Level0Briefed)
        {
            _world.Faith.Say(
                "Start at the reception station. E interacts. FAITH will explain the rest.",
                6f,
                priority: 90,
                instruction: true);
        }
        else if (floorId == FloorId.Level69)
        {
            _world.Faith.Say(
                "The preserve is safe. Visit the collection when you are ready to choose a loadout.",
                6f,
                priority: 90,
                instruction: true);
        }
        else if (floorId == FloorId.Level68)
        {
            _world.Faith.Say(
                "WASD and the left stick move. Mouse and right stick aim. Noise changes what hears you.",
                6f,
                priority: 90,
                instruction: true);
        }

        RefreshPresentation();
    }

    private void SaveCampaign()
    {
        _world.CaptureTo(_campaign);
        _saveStore.Save(_campaign);
        _status = $"Checkpoint saved to {_saveStore.Path}.";
    }

    private void LoadCampaign()
    {
        var loaded = _saveStore.TryLoad();
        if (loaded is null)
        {
            _status = "No checkpoint exists yet. F5 saves the current facility state.";
            return;
        }

        _campaign = loaded;
        _world.Reset(_campaign);
        StartFloor(_campaign.CurrentFloor, preservePosition: true);
        _status = $"Checkpoint restored: {ElevatorSystem.DisplayFloor(_campaign.CurrentFloor)}.";
    }

    private void TogglePause()
    {
        if (_shell.IsPaused || _shell.IsModalOpen)
        {
            _shell.IsPaused = false;
            _shell.DismissModal();
            _status = "Session resumed.";
            return;
        }

        _shell.IsPaused = true;
        _shell.ShowModal(
            new Border
            {
                Background = GraphicalProfile.SurfaceBrush,
                BorderBrush = GraphicalProfile.BorderBrush,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(28),
                Child = new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "PAUSED",
                            FontSize = 26,
                            FontWeight = FontWeight.Bold,
                            Foreground = GraphicalProfile.TextBrush,
                        },
                        new TextBlock
                        {
                            Text = "Escape resumes. F5 saves. F9 restores the last checkpoint.",
                            FontSize = 14,
                            Foreground = GraphicalProfile.MutedBrush,
                        },
                    },
                },
            });
    }

    private void OnModalDismissed(object? sender, EventArgs e)
    {
        _shell.IsPaused = false;
        _input.ConsumeEdges();
        _status = "Session resumed.";
    }

    private void UpdateCamera(Vector3 aim, float dt)
    {
        var lookAhead = aim.LengthSquaredXz() > 0.01f
            ? aim.NormalizeXz() * 2.1f
            : Vector3.Zero;
        var target = _world.Frank.Position + lookAhead;
        var blend = Math.Clamp(dt * 8f, 0f, 1f);
        _scene.Camera.Position = Vector3.Lerp(_scene.Camera.Position, target, blend);
        _scene.Camera.Position = new Vector3(
            _scene.Camera.Position.X,
            0f,
            _scene.Camera.Position.Z);
        _scene.Camera.ClearColor = _world.Lighting.AmbientColor;
    }

    private void RefreshPresentation()
    {
        _presenter.Sync(
            _scene,
            _world,
            _viewport.PixelWidth,
            _viewport.PixelHeight);
        _hud.Refresh(_world, _campaign, _status, _recording);
    }
}
