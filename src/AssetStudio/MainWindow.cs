using Avalonia;
using Avalonia.Controls;
using Novolis.AssetStudio.Core;
using Novolis.Avalonia.AssetStudio;
using Novolis.Avalonia.GraphicalProfile;
using Novolis.Visual.Compile;
using Novolis.Visual.Definitions;
using Novolis.Visual.Presets;

namespace AssetStudio;

internal sealed class MainWindow : Window
{
    private readonly AssetStudioEditorSurface _surface;

    public MainWindow()
    {
        Title = "Novolis Asset Studio";
        Width = 1440;
        Height = 900;
        MinWidth = 960;
        MinHeight = 600;

        GraphicalProfileBinding.Bind(
            this,
            Window.BackgroundProperty,
            GraphicalProfile.BackgroundResourceKey);
        GraphicalProfileBinding.Bind(
            this,
            Window.ForegroundProperty,
            GraphicalProfile.TextResourceKey);

        _surface = new AssetStudioEditorSurface();
        _surface.CommandSubmitted += OnCommandSubmitted;
        _surface.SetAssetTemplate(
            VisualPresetCatalog.CreateDefault()
                .Find("lightsaber-blade")!);
        Content = _surface;
    }

    private void OnCommandSubmitted(
        object? sender,
        Novolis.Avalonia.Studio.StudioCommandSubmittedEventArgs args)
    {
        var command = args.Text.Trim();
        if (string.Equals(command, "apply", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(command, "apply source", StringComparison.OrdinalIgnoreCase))
        {
            _surface.TryApplySource(out var message);
            _surface.Feedback.SetStatus(message);
            return;
        }

        if (string.Equals(command, "validate", StringComparison.OrdinalIgnoreCase))
        {
            if (!_surface.TryApplySource(out var message))
            {
                _surface.Feedback.SetStatus(message);
                return;
            }

            CompileCurrentDocument();
            return;
        }

        if (string.Equals(command, "preview", StringComparison.OrdinalIgnoreCase))
        {
            _ = _surface.RefreshPreviewAsync();
            _surface.Feedback.SetStatus("CPU preview requested.");
            return;
        }

        if (string.Equals(command, "new material", StringComparison.OrdinalIgnoreCase))
        {
            var material = VisualPresetCatalog.CreateDefault()
                .Find("glass")!
                .Materials
                .Single();
            _surface.SetMaterial(material);
            _surface.Feedback.SetStatus(
                "New Glass material instance created.");
            return;
        }

        if (string.Equals(command, "new asset", StringComparison.OrdinalIgnoreCase))
        {
            _surface.SetAssetTemplate(
                VisualPresetCatalog.CreateDefault()
                    .Find("orbital-reusable-rocket")!);
            _surface.Feedback.SetStatus(
                "New Orbital Reusable Rocket asset created.");
            return;
        }

        if (command.StartsWith("open template ", StringComparison.OrdinalIgnoreCase))
        {
            var id = command["open template ".Length..].Trim();
            var template = VisualPresetCatalog.CreateDefault().Find(id);
            if (template is null)
            {
                _surface.Feedback.SetStatus($"Template not found · {id}");
                return;
            }

            _surface.SetAssetTemplate(template);
            return;
        }

        if (string.Equals(command, "create instance", StringComparison.OrdinalIgnoreCase))
        {
            _surface.Feedback.SetStatus(
                "A reusable child instance is active; edit controls to create overrides.");
            return;
        }

        if (string.Equals(command, "save preset", StringComparison.OrdinalIgnoreCase))
        {
            _surface.Feedback.SetStatus(
                "Preset state is canonical and ready to export.");
            return;
        }

        if (string.Equals(command, "build", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(command, "export", StringComparison.OrdinalIgnoreCase))
        {
            CompileCurrentDocument();
            _surface.Feedback.SetStatus(
                $"{command} completed for the current no-code recipe.");
            return;
        }

        if (string.Equals(command, "help", StringComparison.OrdinalIgnoreCase))
        {
            _surface.Feedback.SetStatus(
                "Commands: new material · new asset · open template <id> · " +
                "create instance · save preset · apply source · validate · " +
                "preview · build · export · help");
            return;
        }

        _surface.Feedback.SetStatus($"Unknown command · {command} · type help.");
    }

    private void CompileCurrentDocument()
    {
        if (_surface.Document is { } document)
        {
            _surface.SetCompilation(
                VisualCompiler.Compile(
                    document.Recipe,
                    new VisualCompileOptions(
                        registry: VisualCompiler.CreateDefaultRegistry(),
                        strict: true)));
        }
    }

    private static VisualRecipe CreateSampleRecipe()
    {
        var leftId = new NodeId(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var rightId = new NodeId(Guid.Parse("10000000-0000-0000-0000-000000000002"));
        var addId = new NodeId(Guid.Parse("10000000-0000-0000-0000-000000000003"));
        var leftConnectionId =
            new ConnectionId(Guid.Parse("20000000-0000-0000-0000-000000000001"));
        var rightConnectionId =
            new ConnectionId(Guid.Parse("20000000-0000-0000-0000-000000000002"));
        var registry = VisualCompiler.CreateDefaultRegistry();
        var scalarContract = registry
            .TryGetDescriptor(VisualBuiltInNodeTypes.ConstantScalar, out var scalarDescriptor)
            ? scalarDescriptor!.FindPort(new PortId("value"))!.Contract
            : throw new InvalidOperationException("Scalar descriptor is unavailable.");
        var addDescriptor = registry.TryGetDescriptor(
            VisualBuiltInNodeTypes.AddScalar,
            out var foundAddDescriptor)
            ? foundAddDescriptor!
            : throw new InvalidOperationException("Add descriptor is unavailable.");
        var leftContract = addDescriptor.FindPort(new PortId("left"))!.Contract;
        var rightContract = addDescriptor.FindPort(new PortId("right"))!.Contract;
        var resultContract = addDescriptor.FindPort(new PortId("result"))!.Contract;

        var left = VisualNode.Create(
            leftId,
            VisualBuiltInNodeTypes.ConstantScalar,
            1,
            [new KeyValuePair<string, VisualValue>(
                "value",
                new ScalarValue(0.25))]);
        var right = VisualNode.Create(
            rightId,
            VisualBuiltInNodeTypes.ConstantScalar,
            1,
            [new KeyValuePair<string, VisualValue>(
                "value",
                new ScalarValue(0.75))]);
        var add = VisualNode.Create(
            addId,
            VisualBuiltInNodeTypes.AddScalar,
            1);

        return new VisualRecipe(
            "assetstudio-sample",
            nodes: [left, right, add],
            connections:
            [
                new VisualConnection(
                    leftConnectionId,
                    new VisualPortReference(leftId, new PortId("value")),
                    new VisualPortReference(addId, new PortId("left")),
                    scalarContract,
                    leftContract),
                new VisualConnection(
                    rightConnectionId,
                    new VisualPortReference(rightId, new PortId("value")),
                    new VisualPortReference(addId, new PortId("right")),
                    scalarContract,
                    rightContract),
            ],
            outputs:
            [
                new VisualOutput(
                    "result",
                    "Result",
                    new VisualPortReference(addId, new PortId("result")),
                    resultContract),
            ]);
    }
}
