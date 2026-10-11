using Avalonia;
using Avalonia.Controls;
using Novolis.AssetStudio.Core;
using Novolis.Avalonia.AssetStudio;
using Novolis.Avalonia.GraphicalProfile;
using Novolis.Visual.Compile;
using Novolis.Visual.Definitions;

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
        _surface.SetDocument(new AssetStudioSemanticState(CreateSampleRecipe()));
        _surface.SetCompilation(
            VisualCompiler.Compile(
                _surface.Document!.Recipe,
                new VisualCompileOptions(
                    registry: VisualCompiler.CreateDefaultRegistry(),
                    strict: true)));
        Content = _surface;
    }

    private void OnCommandSubmitted(
        object? sender,
        Novolis.Avalonia.Studio.StudioCommandSubmittedEventArgs args)
    {
        if (string.Equals(args.Text, "validate", StringComparison.OrdinalIgnoreCase))
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

            return;
        }

        _surface.Feedback.SetStatus(
            $"Command staged · {args.Text} · use Validate to refresh diagnostics.");
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
