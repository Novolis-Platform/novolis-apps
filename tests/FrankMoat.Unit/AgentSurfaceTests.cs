using System.Numerics;
using FrankMoat.Agent;
using FrankMoat.Input;
using Novolis.Agent.Surface;
using Novolis.Rendering.Planar;

namespace FrankMoat.Unit;

public sealed class AgentSurfaceTests
{
    [Test]
    public async Task Agent_contract_exposes_gameplay_actions()
    {
        var definition = AgentSurfaceDefinition.From<IFrankMoatAgentSurface>();
        var actionIds = definition.Actions.Select(action => action.Id).ToArray();

        await Assert.That(actionIds).Contains(FrankMoatAgentActions.Control);
        await Assert.That(actionIds).Contains(FrankMoatAgentActions.Interact);
        await Assert.That(actionIds).Contains(FrankMoatAgentActions.Weapon);
        await Assert.That(definition.HttpEnableEnv).IsEqualTo("NOVOLIS_FRANK_MOAT_AGENT_HTTP");
    }

    [Test]
    public async Task Agent_controls_override_human_axes_without_removing_controller_support()
    {
        var scene = new PlanarScene();
        var input = new FrankInputBuffer();
        var agent = new FrankAgentInput(
            MoveX: -1f,
            MoveZ: 0f,
            HasMove: true,
            AimX: 0f,
            AimZ: -1f,
            HasAim: true,
            RunHeld: true,
            FireHeld: true,
            SecondaryHeld: false,
            ShootPressed: true,
            ReloadPressed: false,
            InteractPressed: false,
            MeleePressed: false,
            PreviousWeaponPressed: false,
            MapPressed: false,
            InspectPressed: false,
            PausePressed: false,
            SavePressed: false,
            LoadPressed: false,
            ControlModePressed: false,
            WeaponSlot: 0,
            ExpiresAtUtc: DateTime.UtcNow.AddSeconds(1));

        var frame = input.BuildFrame(
            scene,
            new Vector2(400f, 300f),
            800,
            600,
            Vector3.Zero,
            Vector3.UnitZ,
            new WindowsGamepadSnapshot(
                MoveX: 1f,
                MoveZ: 1f,
                AimX: 1f,
                AimZ: 0f,
                LeftTrigger: 0f,
                RightTrigger: 0f,
                Buttons: GamepadButtons.None,
                Pressed: GamepadButtons.None,
                RightTriggerWasPressed: false),
            agent);

        await Assert.That(frame.MoveX).IsEqualTo(-1f);
        await Assert.That(frame.MoveZ).IsEqualTo(0f);
        await Assert.That(frame.AimWorld).IsEqualTo(-Vector3.UnitZ);
        await Assert.That(frame.RunHeld).IsTrue();
        await Assert.That(frame.ShootHeld).IsTrue();
        await Assert.That(frame.ShootPressed).IsTrue();
    }
}
