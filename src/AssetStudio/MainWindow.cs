using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.Text;
using Novolis.AssetStudio.Authoring;
using Novolis.AssetStudio.Export;
using Novolis.Avalonia.AssetStudio;
using Novolis.Avalonia.GraphicalProfile;
using Novolis.Visual.Compile;
using Novolis.Visual.Definitions;
using Novolis.Visual.Assets;
using Novolis.Visual.Materials;
using Novolis.Visual.Presets;

namespace AssetStudio;

internal sealed class MainWindow : Window
{
    private readonly AssetStudioEditorSurface _surface;
    private string? _currentFilePath;

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
        _surface.SetMaterial(
            AssetStudioBlankDocumentFactory.CreateBlankMaterial());
        Content = _surface;
    }

    private async void OnCommandSubmitted(
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
            _surface.SetMaterial(
                AssetStudioBlankDocumentFactory.CreateBlankMaterial());
            _currentFilePath = null;
            _surface.Feedback.SetStatus(
                "New blank material created.");
            return;
        }

        if (string.Equals(command, "new asset", StringComparison.OrdinalIgnoreCase))
        {
            _surface.SetAsset(
                AssetStudioBlankDocumentFactory.CreateBlankAsset());
            _currentFilePath = null;
            _surface.Feedback.SetStatus(
                "New blank asset assembly created.");
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
            if (_surface.TryCreateInstance())
            {
                await SaveInstanceAsync();
            }
            else
            {
                _surface.Feedback.FlashError(
                    "Create Instance requires a material or asset document.");
            }
            return;
        }

        if (string.Equals(command, "open", StringComparison.OrdinalIgnoreCase))
        {
            await OpenAsync();
            return;
        }

        if (string.Equals(command, "save", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(command, "save preset", StringComparison.OrdinalIgnoreCase))
        {
            await SaveAsync();
            return;
        }

        if (string.Equals(command, "undo", StringComparison.OrdinalIgnoreCase))
        {
            _surface.Feedback.SetStatus(
                _surface.TryUndo() ? "Undo applied." : "Nothing to undo.");
            return;
        }

        if (string.Equals(command, "redo", StringComparison.OrdinalIgnoreCase))
        {
            _surface.Feedback.SetStatus(
                _surface.TryRedo() ? "Redo applied." : "Nothing to redo.");
            return;
        }

        if (string.Equals(command, "build", StringComparison.OrdinalIgnoreCase))
        {
            CompileCurrentDocument();
            _surface.Feedback.SetStatus(
                "Build completed for the current generic document.");
            return;
        }

        if (string.Equals(command, "export", StringComparison.OrdinalIgnoreCase))
        {
            await ExportAsync();
            return;
        }

        if (string.Equals(command, "help", StringComparison.OrdinalIgnoreCase))
        {
            _surface.Feedback.SetStatus(
                "Commands: new material · new asset · open · save · " +
                "open template <id> · create instance · undo · redo · " +
                "apply source · validate · preview · build · export · help");
            return;
        }

        _surface.Feedback.SetStatus($"Unknown command · {command} · type help.");
    }

    private void CompileCurrentDocument()
    {
        if (_surface.CurrentDocument is { } document)
        {
            var options = new VisualCompileOptions(
                registry: VisualCompiler.CreateDefaultRegistry(),
                strict: true);
            var compilation = document.DocumentKind switch
            {
                AssetStudioDocumentKind.Material when document.Material is { } material =>
                    VisualMaterialCompiler.CompileMaterial(
                        material,
                        document.MaterialInstance,
                        options),
                AssetStudioDocumentKind.Asset when document.Asset is { } asset =>
                    VisualMaterialCompiler.CompileAsset(
                        asset,
                        document.AssetInstance,
                        options),
                _ => VisualCompiler.Compile(document.Recipe, options),
            };
            _surface.SetCompilation(
                compilation);
        }
    }

    private async Task OpenAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Asset Studio documents")
                    {
                        Patterns = ["*.json", "*.novmaterial.json", "*.novasset.json"],
                    },
                ],
                Title = "Open Asset Studio document",
            });
        var file = files.FirstOrDefault();
        if (file is null)
        {
            return;
        }

        try
        {
            var path = file.Path.LocalPath;
            var document = AssetStudioModulePersistence.DeserializeDocument(
                await File.ReadAllTextAsync(path, Encoding.UTF8),
                registry: VisualCompiler.CreateDefaultRegistry());
            _surface.SetDocument(document);
            _currentFilePath = path;
            _surface.Feedback.SetStatus($"Opened · {Path.GetFileName(path)}");
        }
        catch (Exception exception)
        {
            _surface.Feedback.FlashError($"Open failed · {exception.Message}");
        }
    }

    private async Task SaveAsync()
    {
        if (_surface.CurrentDocument is not { } document)
        {
            _surface.Feedback.FlashError("There is no document to save.");
            return;
        }

        var path = _currentFilePath;
        if (string.IsNullOrWhiteSpace(path))
        {
            var file = await StorageProvider.SaveFilePickerAsync(
                new FilePickerSaveOptions
                {
                    SuggestedFileName = document.DocumentKind switch
                    {
                        AssetStudioDocumentKind.Material => "material.novmaterial.json",
                        AssetStudioDocumentKind.Asset => "asset.novasset.json",
                        _ => "recipe.novvisual.json",
                    },
                    Title = "Save Asset Studio document",
                });
            path = file?.Path.LocalPath;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            await File.WriteAllTextAsync(
                path,
                AssetStudioModulePersistence.SerializeDocument(document),
                Encoding.UTF8);
            _currentFilePath = path;
            _surface.Feedback.SetStatus($"Saved · {Path.GetFileName(path)}");
        }
        catch (Exception exception)
        {
            _surface.Feedback.FlashError($"Save failed · {exception.Message}");
        }
    }

    private async Task SaveInstanceAsync()
    {
        if (_surface.CurrentDocument is not { } document ||
            (document.Material is null && document.Asset is null))
        {
            _surface.Feedback.FlashError(
                "Create Instance requires a material or asset document.");
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                SuggestedFileName = document.DocumentKind ==
                                    AssetStudioDocumentKind.Material
                    ? "material.instance.json"
                    : "asset.instance.json",
                Title = "Save reusable instance",
            });
        if (file is null)
        {
            return;
        }

        try
        {
            var content = document.Material is { } material
                ? AssetStudioModulePersistence.SerializeMaterialInstance(
                    document.MaterialInstance ??
                    new MaterialInstance(material.MaterialId + ".instance", material))
                : AssetStudioModulePersistence.SerializeAssetInstance(
                    document.AssetInstance ??
                    new AssetInstance(
                        document.Asset!.AssetId + ".instance",
                        document.Asset));
            await File.WriteAllTextAsync(
                file.Path.LocalPath,
                content,
                Encoding.UTF8);
            _surface.Feedback.SetStatus(
                $"Instance saved · {Path.GetFileName(file.Path.LocalPath)}");
        }
        catch (Exception exception)
        {
            _surface.Feedback.FlashError(
                $"Instance save failed · {exception.Message}");
        }
    }

    private async Task ExportAsync()
    {
        if (_surface.CurrentDocument is not { } document)
        {
            _surface.Feedback.FlashError("There is no document to export.");
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                SuggestedFileName = "assetstudio-definition.json",
                Title = "Export Asset Studio definition",
            });
        if (file is null)
        {
            return;
        }

        var profile = document.DocumentKind ==
                      AssetStudioDocumentKind.Material
            ? AssetStudioExportProfile.MaterialModule
            : document.DocumentKind == AssetStudioDocumentKind.Asset
                ? AssetStudioExportProfile.AssetPackage
                : AssetStudioExportProfile.Definition;
        try
        {
            var result = new AssetStudioExporter().Export(
                new AssetStudioExportRequest(
                    document,
                    profile,
                    material: document.Material,
                    asset: document.Asset));
            await File.WriteAllTextAsync(
                file.Path.LocalPath,
                result.Manifest.ToCanonicalJson(),
                Encoding.UTF8);
            _surface.Feedback.SetStatus(
                $"Exported {result.Artifacts.Length} artifact(s) · " +
                Path.GetFileName(file.Path.LocalPath));
        }
        catch (Exception exception)
        {
            _surface.Feedback.FlashError($"Export failed · {exception.Message}");
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
