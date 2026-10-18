using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Agent.Core;
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
    private readonly IGamepadReader _gamepad;
    private readonly FixedStepClock _clock = new();
    private readonly ElevatorSystem _elevator = new();
    private readonly CampaignSaveStore _saveStore = new();
    private readonly object _simulationGate = new();
    private CampaignState _campaign = new();
    private FrankAgentInput _agentInput;
    private long _agentSequence;
    private FounderRecording? _recording;
    private bool _sessionPaused;
    private string _status = "Morning round in progress.";
    private float _targetWorldUnitsPerPixel = 0.021f;
    private bool _disposed;

    internal event EventHandler? ExitRequested;

    public FrankMoatSession(
        PlanarSceneControl viewport,
        GameShell shell,
        FrankMoatHud hud,
        IGamepadReader? gamepad = null)
    {
        _viewport = viewport;
        _shell = shell;
        _hud = hud;
        _gamepad = gamepad ?? new WindowsGamepadReader();
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
        lock (_simulationGate)
        {
            OnFrameUpdatingCore(e);
        }
    }

    private void OnFrameUpdatingCore(PlanarFrameEventArgs e)
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
            _world.Frank.Facing,
            gamepad,
            ReadAgentInput());

        if (frame.ControlModePressed)
        {
            _input.ToggleControlMode();
            _status = $"Control mode: {_input.ControlMode}.";
        }

        ApplyZoom(frame.ZoomTicks, e.DeltaSeconds);

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
            SaveCampaign(frame.SaveName);
        }
        else if (frame.LoadPressed)
        {
            LoadCampaign(frame.LoadName);
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

        if (floorId == FloorId.Level69 && !_campaign.Level69LoadoutChosen)
        {
            _campaign.CurrentObjectiveId = "choose-loadout";
        }
        else if (floorId == FloorId.Level68 && !_campaign.Level68IncidentResolved)
        {
            _campaign.CurrentObjectiveId = "containment-clearance";
        }

        _world.LoadFloor(floor, _campaign);
        _world.ApplyCollision(_scene);
        _presenter.BuildStatic(_scene, _world);
        _targetWorldUnitsPerPixel = 0.021f;
        _scene.Camera.WorldUnitsPerPixel = _targetWorldUnitsPerPixel;
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

    private void SaveCampaign(string? name = null)
    {
        _world.CaptureTo(_campaign);
        _saveStore.Save(_campaign, name);
        _status = string.IsNullOrWhiteSpace(name)
            ? $"Quick checkpoint saved to {_saveStore.Path}."
            : $"Named checkpoint '{name.Trim()}' saved.";
    }

    private void LoadCampaign(string? name = null)
    {
        var loaded = _saveStore.TryLoad(name);
        if (loaded is null)
        {
            _status = string.IsNullOrWhiteSpace(name)
                ? "No quick checkpoint exists yet. F5 saves the current facility state."
                : $"Named checkpoint '{name.Trim()}' was not found.";
            return;
        }

        _campaign = loaded;
        _world.Reset(_campaign);
        StartFloor(_campaign.CurrentFloor, preservePosition: true);
        _status = string.IsNullOrWhiteSpace(name)
            ? $"Quick checkpoint restored: {ElevatorSystem.DisplayFloor(_campaign.CurrentFloor)}."
            : $"Named checkpoint '{name.Trim()}' restored: {ElevatorSystem.DisplayFloor(_campaign.CurrentFloor)}.";
    }

    private void TogglePause()
    {
        if (_shell.IsPaused || _shell.IsModalOpen)
        {
            _shell.IsPaused = false;
            _sessionPaused = false;
            _shell.DismissModal();
            _status = "Session resumed.";
            return;
        }

        _shell.IsPaused = true;
        _sessionPaused = true;
        var nameBox = new TextBox
        {
            PlaceholderText = "checkpoint name",
            Width = 300,
        };
        var saveQuick = new Button
        {
            Content = "SAVE CHECKPOINT",
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        var loadQuick = new Button
        {
            Content = "RESTORE CHECKPOINT",
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        var saveNamed = new Button
        {
            Content = "SAVE NAMED CHECKPOINT",
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        var loadNamed = new Button
        {
            Content = "RESTORE NAMED CHECKPOINT",
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        var exit = new Button
        {
            Content = "EXIT GAME",
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };

        void CloseMenu()
        {
            _shell.DismissModal();
            RefreshPresentation();
        }

        saveQuick.Click += (_, _) =>
        {
            CloseMenu();
            SaveCampaign();
            RefreshPresentation();
        };
        loadQuick.Click += (_, _) =>
        {
            CloseMenu();
            LoadCampaign();
            RefreshPresentation();
        };
        saveNamed.Click += (_, _) =>
        {
            CloseMenu();
            SaveCampaign(nameBox.Text);
            RefreshPresentation();
        };
        loadNamed.Click += (_, _) =>
        {
            CloseMenu();
            LoadCampaign(nameBox.Text);
            RefreshPresentation();
        };
        exit.Click += (_, _) =>
        {
            _shell.DismissModal();
            ExitRequested?.Invoke(this, EventArgs.Empty);
        };

        _shell.ShowModal(
            new Border
            {
                Background = GraphicalProfile.SurfaceBrush,
                BorderBrush = GraphicalProfile.BorderBrush,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(28),
                Width = 440,
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
                            Text = "Escape resumes. F5/F9 use the quick checkpoint.",
                            FontSize = 14,
                            Foreground = GraphicalProfile.MutedBrush,
                        },
                        saveQuick,
                        loadQuick,
                        new Separator(),
                        new TextBlock
                        {
                            Text = "Named checkpoint",
                            FontSize = 13,
                            Foreground = GraphicalProfile.MutedBrush,
                        },
                        nameBox,
                        saveNamed,
                        loadNamed,
                        new Separator(),
                        exit,
                    },
                },
            });
    }

    private void OnModalDismissed(object? sender, EventArgs e)
    {
        _shell.IsPaused = false;
        _sessionPaused = false;
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

    private void ApplyZoom(int ticks, float dt)
    {
        if (ticks != 0)
        {
            _targetWorldUnitsPerPixel = Math.Clamp(
                _targetWorldUnitsPerPixel * (ticks > 0 ? 0.88f : 1.14f),
                0.011f,
                0.032f);
        }

        var blend = Math.Clamp(dt * 10f, 0f, 1f);
        _scene.Camera.WorldUnitsPerPixel +=
            (_targetWorldUnitsPerPixel - _scene.Camera.WorldUnitsPerPixel) * blend;
    }

    private void RefreshPresentation()
    {
        _presenter.Sync(
            _scene,
            _world,
            _campaign,
            _viewport.PixelWidth,
            _viewport.PixelHeight);
        _hud.Refresh(_world, _campaign, _status, _recording, _input.ControlMode);
    }

    internal void QueueAgentInput(FrankAgentInput input)
    {
        lock (_simulationGate)
        {
            _agentInput = input;
        }
    }

    internal void ReleaseAgentInput()
    {
        lock (_simulationGate)
        {
            _agentInput = FrankAgentInput.Empty;
        }
    }

    internal AgentSnapshot CreateAgentSnapshot()
    {
        lock (_simulationGate)
        {
            var hints = _world.GetInteractionHints(_campaign);
            var status = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["floor"] = _world.Floor.Id.ToString(),
                ["floorName"] = _world.Floor.Name,
                ["objective"] = _world.ObjectiveText(_campaign),
                ["position"] = FormatVector(_world.Frank.Position),
                ["facing"] = FormatVector(_world.Frank.Facing),
                ["health"] = _world.Frank.Health.ToString(),
                ["armor"] = _world.Frank.Armor.ToString(),
                ["weapon"] = _world.Weapon.Spec.Id.ToString(),
                ["ammo"] = $"{_world.Weapon.RoundsInGun}/{_world.Weapon.Reserve}",
                ["kills"] = _world.Kills.ToString(),
                ["enemies"] = _world.Undead.Count.ToString(),
                ["noiseRadius"] = _world.NoiseRadius.ToString("0.##", CultureInfo.InvariantCulture),
                ["controlMode"] = _input.ControlMode.ToString(),
                ["faith"] = _world.Faith.Text,
                ["status"] = _status,
                ["map"] = _world.MapText(),
                ["recording"] = _recording?.Title ?? string.Empty,
                ["nearestInteraction"] = _world.NearestInteraction(_campaign)?.Id ?? string.Empty,
            };

            foreach (var hint in hints)
            {
                status[$"hint.{hint.Id}"] =
                    $"{FormatVector(hint.Position)}|{hint.Label}|{hint.IsAvailable}|{hint.LockReason}";
            }

            for (var index = 0; index < _world.Undead.Count; index++)
            {
                var enemy = _world.Undead[index];
                status[$"enemy.{index}"] =
                    $"{enemy.Kind}|{FormatVector(enemy.Position)}|{enemy.Health}|{enemy.Alerted}";
            }

            var underway = _elevator.State != ElevatorState.Idle;
            return new AgentSnapshot
            {
                Sequence = ++_agentSequence,
                SeedHash = _campaign.WorldSeed.ToString(),
                HubId = _world.Floor.Id.ToString(),
                HubName = _world.Floor.Name,
                PauseReason = _sessionPaused
                    ? "Paused"
                    : "Running",
                StatusLines = status,
                Underway = underway,
                TravelTargetSystemId = _elevator.Destination?.ToString(),
                Complete = _world.IsCleared,
                SoftFail = _world.Frank.Health <= 0,
                MapVisible = _world.MapVisible,
                Actions = [],
            };
        }
    }

    internal AgentCommandResult ExecuteAgentAction(AgentCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var actionId = command.ActionId?.Trim() ?? string.Empty;
        var action = actionId.ToLowerInvariant();

        return action switch
        {
            Agent.FrankMoatAgentActions.Control => QueueAgentControl(command),
            Agent.FrankMoatAgentActions.Release => ReleaseAgentControl(command),
            Agent.FrankMoatAgentActions.Interact => QueueAgentEdge(command, interact: true),
            Agent.FrankMoatAgentActions.Fire => QueueAgentEdge(command, shoot: true),
            Agent.FrankMoatAgentActions.Reload => QueueAgentEdge(command, reload: true),
            Agent.FrankMoatAgentActions.Melee => QueueAgentEdge(command, melee: true),
            Agent.FrankMoatAgentActions.Weapon => QueueAgentWeapon(command),
            Agent.FrankMoatAgentActions.Map => QueueAgentEdge(command, map: true),
            Agent.FrankMoatAgentActions.Inspect => QueueAgentEdge(command, inspect: true),
            Agent.FrankMoatAgentActions.Pause => QueueAgentEdge(command, pause: true),
            Agent.FrankMoatAgentActions.Save => QueueAgentEdge(
                command,
                save: true,
                saveName: command.Get("name")),
            Agent.FrankMoatAgentActions.Load => QueueAgentEdge(
                command,
                load: true,
                loadName: command.Get("name")),
            Agent.FrankMoatAgentActions.ControlMode => QueueAgentEdge(command, controlMode: true),
            Agent.FrankMoatAgentActions.Exit => RequestExit(command),
            _ => new AgentCommandResult
            {
                Ok = false,
                ActionId = actionId,
                ErrorCode = "unknown_action",
                Message = $"Unknown Frank Moat action '{actionId}'.",
                Snapshot = CreateAgentSnapshot(),
            },
        };
    }

    private AgentCommandResult RequestExit(AgentCommand command)
    {
        ExitRequested?.Invoke(this, EventArgs.Empty);
        return AgentResult(command.ActionId, "Exit requested.");
    }

    private AgentCommandResult QueueAgentControl(AgentCommand command)
    {
        var moveX = ReadFloat(command, "moveX", 0f);
        var moveZ = ReadFloat(command, "moveZ", 0f);
        var aimX = ReadFloat(command, "aimX", 0f);
        var aimZ = ReadFloat(command, "aimZ", 0f);
        var hasAim = command.Params.ContainsKey("aimX") || command.Params.ContainsKey("aimZ");
        var duration = Math.Clamp(ReadFloat(command, "duration", 0.25f), 0.05f, 10f);
        var input = new FrankAgentInput(
            Math.Clamp(moveX, -1f, 1f),
            Math.Clamp(moveZ, -1f, 1f),
            command.Params.ContainsKey("moveX") || command.Params.ContainsKey("moveZ"),
            aimX,
            aimZ,
            hasAim,
            ReadBool(command, "run"),
            ReadBool(command, "fire"),
            ReadBool(command, "secondary"),
            false,
            ReadBool(command, "reload"),
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            0,
            DateTime.UtcNow).WithDuration(duration);

        QueueAgentInput(input);
        return AgentResult(Agent.FrankMoatAgentActions.Control, $"Control queued for {duration:0.##} seconds.");
    }

    private AgentCommandResult ReleaseAgentControl(AgentCommand command)
    {
        ReleaseAgentInput();
        return AgentResult(Agent.FrankMoatAgentActions.Release, "Agent control released.");
    }

    private AgentCommandResult QueueAgentEdge(
        AgentCommand command,
        bool shoot = false,
        bool reload = false,
        bool interact = false,
        bool melee = false,
        bool map = false,
        bool inspect = false,
        bool pause = false,
        bool save = false,
        bool load = false,
        bool controlMode = false,
        string? saveName = null,
        string? loadName = null)
    {
        var hasMove = command.Params.ContainsKey("moveX")
            || command.Params.ContainsKey("moveZ");
        var hasAim = shoot
            && (command.Params.ContainsKey("aimX") || command.Params.ContainsKey("aimZ"));
        var duration = Math.Clamp(ReadFloat(command, "duration", 0.2f), 0.05f, 10f);
        QueueAgentInput(
            new FrankAgentInput(
                Math.Clamp(ReadFloat(command, "moveX", 0f), -1f, 1f),
                Math.Clamp(ReadFloat(command, "moveZ", 0f), -1f, 1f),
                hasMove,
                ReadFloat(command, "aimX", 0f),
                ReadFloat(command, "aimZ", 0f),
                hasAim,
                ReadBool(command, "run"),
                false,
                false,
                shoot,
                reload,
                interact,
                melee,
                false,
                map,
                inspect,
                pause,
                save,
                load,
                controlMode,
                0,
                DateTime.UtcNow)
            {
                SaveName = saveName,
                LoadName = loadName,
            }.WithDuration(duration));

        return AgentResult(command.ActionId, $"Agent action '{command.ActionId}' queued.");
    }

    private AgentCommandResult QueueAgentWeapon(AgentCommand command)
    {
        if (!command.TryGetInt("slot", out var slot) || slot is < 1 or > 5)
        {
            return new AgentCommandResult
            {
                Ok = false,
                ActionId = command.ActionId,
                ErrorCode = "invalid_slot",
                Message = "Weapon slot must be between 1 and 5.",
                Snapshot = CreateAgentSnapshot(),
            };
        }

        QueueAgentInput(
            new FrankAgentInput(
                0f,
                0f,
                false,
                0f,
                0f,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                slot,
                DateTime.UtcNow).WithDuration(0.2f));
        return AgentResult(Agent.FrankMoatAgentActions.Weapon, $"Weapon slot {slot} queued.");
    }

    private AgentCommandResult AgentResult(string actionId, string message) =>
        new()
        {
            Ok = true,
            ActionId = actionId,
            Message = message,
            Snapshot = CreateAgentSnapshot(),
        };

    private FrankAgentInput ReadAgentInput()
    {
        var input = _agentInput;
        if (!input.IsActive)
        {
            _agentInput = FrankAgentInput.Empty;
            return FrankAgentInput.Empty;
        }

        _agentInput = input.WithoutEdges();
        return input;
    }

    private static float ReadFloat(AgentCommand command, string key, float fallback)
    {
        return command.TryGetDouble(key, out var value)
            ? (float)value
            : fallback;
    }

    private static bool ReadBool(AgentCommand command, string key) =>
        command.TryGetBool(key, out var value) && value;

    private static string FormatVector(Vector3 value) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{value.X:0.##},{value.Z:0.##}");
}
