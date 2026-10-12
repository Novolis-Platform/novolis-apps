using FrankMoat.Levels;
using Novolis.Math.Geometry;

namespace FrankMoat.Rendering;

internal sealed class LightingRig
{
    private float _time;
    private float _flashTimer;

    public LightingMode Mode { get; private set; } = LightingMode.Normal;

    public float FlashlightStrength { get; private set; } = 0.9f;

    public float AlarmAngle => _time * 2.9f;

    public float AmbientIntensity => Mode switch
    {
        LightingMode.ElevatorTransit => 0.25f,
        LightingMode.DarkContainment => 0.38f,
        LightingMode.Combat => 0.45f,
        LightingMode.Emergency => 0.5f,
        LightingMode.Preserved => 0.85f,
        _ => 0.68f,
    };

    public Rgba32 AmbientColor => Mode switch
    {
        LightingMode.Lockdown => new Rgba32(28, 22, 28),
        LightingMode.Emergency => new Rgba32(24, 24, 30),
        LightingMode.Preserved => new Rgba32(58, 48, 38),
        LightingMode.DarkContainment => new Rgba32(18, 22, 30),
        LightingMode.Combat => new Rgba32(26, 20, 28),
        LightingMode.ElevatorTransit => new Rgba32(12, 15, 22),
        _ => new Rgba32(30, 34, 42),
    };

    public void SetMode(FloorId floor, bool directive, bool combat, bool elevator)
    {
        Mode = elevator
            ? LightingMode.ElevatorTransit
            : combat
                ? LightingMode.Combat
                : floor switch
                {
                    FloorId.Level69 => LightingMode.Preserved,
                    FloorId.Level68 when directive => LightingMode.DarkContainment,
                    FloorId.Level0 when directive => LightingMode.Emergency,
                    _ => LightingMode.Normal,
                };

        FlashlightStrength = Mode switch
        {
            LightingMode.Preserved => 0.8f,
            LightingMode.Emergency => 1.15f,
            LightingMode.DarkContainment => 1.3f,
            LightingMode.Combat => 1.35f,
            LightingMode.ElevatorTransit => 0.2f,
            _ => 1.1f,
        };
    }

    public void Tick(float dt, bool combatFlash)
    {
        _time += dt;
        _flashTimer = MathF.Max(0f, _flashTimer - dt);
        if (combatFlash)
        {
            _flashTimer = MathF.Max(_flashTimer, 0.14f);
        }
    }

    public Rgba32 FixtureTint(LightFixtureDefinition fixture)
    {
        if (fixture.EmergencyOnly
            && Mode is not LightingMode.Emergency
            && Mode is not LightingMode.Combat
            && Mode is not LightingMode.DarkContainment)
        {
            return new Rgba32(0, 0, 0, 0);
        }

        var flicker = 1f;
        if (Mode is LightingMode.Emergency or LightingMode.Combat or LightingMode.DarkContainment)
        {
            flicker = 0.72f + 0.28f * (0.5f + 0.5f * MathF.Sin(_time * 19f + fixture.Position.X));
        }

        if (_flashTimer > 0f)
        {
            flicker = MathF.Min(1.5f, flicker + _flashTimer * 2.6f);
        }

        var strength = Math.Clamp(fixture.Intensity * flicker, 0f, 1.5f);
        return new Rgba32(
            fixture.Color.R,
            fixture.Color.G,
            fixture.Color.B,
            (byte)Math.Clamp((int)(128f * strength), 0, 220));
    }
}
