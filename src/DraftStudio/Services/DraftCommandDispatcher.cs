using System.Globalization;
using System.Numerics;
using DraftStudio.Commands;
using DraftStudio.Core;
using DraftStudio.Models;
using Novolis.Commands.Expressions;
using Novolis.Math.Geometry;

namespace DraftStudio.Services;

internal enum DraftToolKind
{
    Select,
    Line,
    Circle,
    Rect,
    Spline,
}

internal sealed class DraftCommandDispatcher
{
    private readonly DraftSession _session;
    private readonly DraftCommandBus _bus;
    private readonly DraftSettingsStore _settings;

    public DraftCommandDispatcher(DraftSession session, DraftCommandBus bus, DraftSettingsStore settings)
    {
        _session = session;
        _bus = bus;
        _settings = settings;
    }

    public DraftToolKind ActiveTool { get; private set; } = DraftToolKind.Select;

    public event Action? ToolChanged;

    public event Action? FitRequested;

    public event Action? SaveRequested;

    public event Action? DumpArtifactsRequested;

    public event Action? ElevationChanged;

    public string? TryDispatch(string prompt)
    {
        var parsed = FunctionCallParser.TryParse(prompt);
        if (!parsed.Success)
            return parsed.Message ?? "Could not parse command.";

        var call = parsed.Call!;
        var name = call.Name.ToLowerInvariant();

        if (!call.HasParentheses || call.Arguments.Count == 0)
        {
            return name switch
            {
                "line" => EnterTool(DraftToolKind.Line),
                "circle" => EnterTool(DraftToolKind.Circle),
                "rect" or "rectangle" => EnterTool(DraftToolKind.Rect),
                "spline" => EnterTool(DraftToolKind.Spline),
                "select" => EnterTool(DraftToolKind.Select),
                "undo" => Do(() => _bus.Undo()),
                "redo" => Do(() => _bus.Redo()),
                "delete" => DeleteSelection(),
                "fit" => Do(() => FitRequested?.Invoke()),
                "save" => Do(() => SaveRequested?.Invoke()),
                "dump" or "dumpall" or "dumpmodel" or "dumpui" => Do(() => DumpArtifactsRequested?.Invoke()),
                "level" or "elevation" or "zlevel" => "Level(y) sets drawing elevation (world Y).",
                "box" => "Box requires arguments: Box(w,h,d) or Box(x,y,z,w,h,d).",
                _ => $"Unknown command '{call.Name}'.",
            };
        }

        return name switch
        {
            "line" => AddLine(call),
            "circle" => AddCircle(call),
            "rect" or "rectangle" => AddRect(call),
            "spline" => AddSpline(call),
            "box" => AddBox(call),
            "cylinder" => AddCylinder(call),
            "sphere" => AddSphere(call),
            "move" => Move(call),
            "level" or "elevation" or "zlevel" => SetLevel(call),
            "save" => Do(() => SaveRequested?.Invoke()),
            "dump" or "dumpall" or "dumpmodel" or "dumpui" => Do(() => DumpArtifactsRequested?.Invoke()),
            "delete" => DeleteSelection(),
            "undo" => Do(() => _bus.Undo()),
            "redo" => Do(() => _bus.Redo()),
            "fit" => Do(() => FitRequested?.Invoke()),
            _ => $"Unknown command '{call.Name}'.",
        };
    }

    public void EmitAdd(CadEntity entity, bool keepTool = false)
    {
        _bus.Execute(new AddEntityCommand(entity));
        if (!keepTool)
            EnterTool(DraftToolKind.Select);
    }

    public string? EnterTool(DraftToolKind tool)
    {
        ActiveTool = tool;
        ToolChanged?.Invoke();
        return null;
    }

    private string? AddLine(FunctionCall call)
    {
        if (!RequireNumbers(call, 4, out var n, out var err))
            return err;
        var y = _settings.Settings.DrawElevation;
        _bus.Execute(new AddEntityCommand(new CadEntity
        {
            Name = NextName("Line"),
            Kind = "line",
            A = CadVec.Plan((float)n[0], (float)n[1], y),
            B = CadVec.Plan((float)n[2], (float)n[3], y),
            Style = new CadStyle { Linetype = "Continuous" },
        }));
        return null;
    }

    private string? AddCircle(FunctionCall call)
    {
        if (!RequireNumbers(call, 3, out var n, out var err))
            return err;
        var y = _settings.Settings.DrawElevation;
        _bus.Execute(new AddEntityCommand(new CadEntity
        {
            Name = NextName("Circle"),
            Kind = "circle",
            Center = CadVec.Plan((float)n[0], (float)n[1], y),
            Radius = (float)n[2],
            Normal = [0f, 1f, 0f],
        }));
        return null;
    }

    private string? AddRect(FunctionCall call)
    {
        if (!RequireNumbers(call, 4, out var n, out var err))
            return err;
        var y = _settings.Settings.DrawElevation;
        _bus.Execute(new AddEntityCommand(new CadEntity
        {
            Name = NextName("Rect"),
            Kind = "rect",
            A = CadVec.Plan((float)n[0], (float)n[1], y),
            B = CadVec.Plan((float)n[2], (float)n[3], y),
            Normal = [0f, 1f, 0f],
        }));
        return null;
    }

    private string? AddSpline(FunctionCall call)
    {
        // Spline(x1,z1,x2,z2,...) flat XZ pairs on current elevation
        if (call.Arguments.Count < 4 || call.Arguments.Count % 2 != 0)
            return "Spline expects an even number of XZ values (at least two points).";

        var y = _settings.Settings.DrawElevation;
        var fit = new List<Vector3>();
        for (var i = 0; i < call.Arguments.Count; i += 2)
        {
            if (call.Arguments[i].Number is not { } x || call.Arguments[i + 1].Number is not { } z)
                return $"Spline arguments {i + 1}/{i + 2} must be numbers.";
            fit.Add(new Vector3((float)x, y, (float)z));
        }

        var (degree, controls, knots, weights) = NurbsCurve.FromFitPoints(fit);
        _bus.Execute(new AddEntityCommand(new CadEntity
        {
            Name = NextName("Spline"),
            Kind = "spline",
            Degree = degree,
            ControlPoints = controls.Select(CadVec.From).ToList(),
            Knots = knots,
            Weights = weights,
            FitPoints = fit.Select(CadVec.From).ToList(),
            Closed = false,
            Normal = [0f, 1f, 0f],
        }));
        return null;
    }

    private string? SetLevel(FunctionCall call)
    {
        if (!RequireNumbers(call, 1, out var n, out var err))
            return err;
        _settings.Settings.DrawElevation = (float)n[0];
        ElevationChanged?.Invoke();
        return null;
    }

    private string? AddBox(FunctionCall call)
    {
        if (call.Arguments.Count == 3)
        {
            if (!RequireNumbers(call, 3, out var n, out var err))
                return err;
            _bus.Execute(new AddEntityCommand(new CadEntity
            {
                Name = NextName("Box"),
                Kind = "box",
                Center = CadVec.Xyz(0, (float)n[1] * 0.5f, 0),
                HalfExtents = [(float)n[0] * 0.5f, (float)n[1] * 0.5f, (float)n[2] * 0.5f],
            }));
            return null;
        }

        if (!RequireNumbers(call, 6, out var m, out var err6))
            return err6;
        _bus.Execute(new AddEntityCommand(new CadEntity
        {
            Name = NextName("Box"),
            Kind = "box",
            Center = CadVec.Xyz((float)m[0], (float)m[1], (float)m[2]),
            HalfExtents = [(float)m[3] * 0.5f, (float)m[4] * 0.5f, (float)m[5] * 0.5f],
        }));
        return null;
    }

    private string? AddCylinder(FunctionCall call)
    {
        if (call.Arguments.Count == 2)
        {
            if (!RequireNumbers(call, 2, out var n, out var err))
                return err;
            _bus.Execute(new AddEntityCommand(new CadEntity
            {
                Name = NextName("Cylinder"),
                Kind = "cylinder",
                Center = CadVec.Xyz(0, (float)n[1] * 0.5f, 0),
                Radius = (float)n[0],
                Height = (float)n[1],
            }));
            return null;
        }

        if (call.Arguments.Count == 5)
        {
            if (!RequireNumbers(call, 5, out var n, out var err))
                return err;
            _bus.Execute(new AddEntityCommand(new CadEntity
            {
                Name = NextName("Cylinder"),
                Kind = "cylinder",
                Center = CadVec.Xyz((float)n[0], (float)n[1], (float)n[2]),
                Radius = (float)n[3],
                Height = (float)n[4],
            }));
            return null;
        }

        return "Cylinder(r,h) or Cylinder(x,y,z,r,h).";
    }

    private string? AddSphere(FunctionCall call)
    {
        if (call.Arguments.Count == 1)
        {
            if (!RequireNumbers(call, 1, out var n, out var err))
                return err;
            _bus.Execute(new AddEntityCommand(new CadEntity
            {
                Name = NextName("Sphere"),
                Kind = "sphere",
                Center = CadVec.Xyz(0, (float)n[0], 0),
                Radius = (float)n[0],
            }));
            return null;
        }

        if (!RequireNumbers(call, 4, out var m, out var err4))
            return err4;
        _bus.Execute(new AddEntityCommand(new CadEntity
        {
            Name = NextName("Sphere"),
            Kind = "sphere",
            Center = CadVec.Xyz((float)m[0], (float)m[1], (float)m[2]),
            Radius = (float)m[3],
        }));
        return null;
    }

    private string? Move(FunctionCall call)
    {
        if (!RequireNumbers(call, 3, out var n, out var err))
            return err;
        if (_session.SelectedId is null)
            return "Nothing selected.";
        _bus.Execute(new MoveEntitiesCommand([_session.SelectedId.Value], (float)n[0], (float)n[1], (float)n[2]));
        return null;
    }

    private string? DeleteSelection()
    {
        if (_session.SelectedId is null)
            return "Nothing selected.";
        _bus.Execute(new DeleteEntitiesCommand([_session.SelectedId.Value]));
        return null;
    }

    private string? Do(Action action)
    {
        action();
        return null;
    }

    private string NextName(string prefix)
    {
        var n = _session.Document.Entities.Count(e =>
            (e.Name ?? string.Empty).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) + 1;
        return $"{prefix} {n}";
    }

    private static bool RequireNumbers(FunctionCall call, int count, out double[] numbers, out string? error)
    {
        numbers = new double[count];
        if (call.Arguments.Count != count)
        {
            error = $"'{call.Name}' expects {count} number argument(s), got {call.Arguments.Count}.";
            return false;
        }

        for (var i = 0; i < count; i++)
        {
            if (call.Arguments[i].Number is not { } value)
            {
                error = $"Argument {i + 1} must be a number (got '{call.Arguments[i].Raw}').";
                numbers = [];
                return false;
            }

            numbers[i] = value;
        }

        error = null;
        return true;
    }

    public static string FormatInvariant(double value) =>
        value.ToString("0.####", CultureInfo.InvariantCulture);
}
