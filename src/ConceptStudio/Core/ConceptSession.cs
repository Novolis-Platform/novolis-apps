using System.Text.Json;
using ConceptStudio.Models;

namespace ConceptStudio.Core;

internal sealed class ConceptSession
{
    private readonly ConceptSettingsStore _settings;
    private readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    public ConceptSession(ConceptSettingsStore settings) => _settings = settings;

    public ConceptDocument Document { get; private set; } = new();

    public int SceneRevision { get; private set; }

    public bool IsDirty { get; private set; }

    public string WorkspacePath => _settings.WorkspacePath;

    public void OpenOrCreateDefault()
    {
        _settings.Load();
        Directory.CreateDirectory(_settings.WorkspacePath);
        var path = _settings.DocumentPath;
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            Document = JsonSerializer.Deserialize<ConceptDocument>(json, _json) ?? CreateShipTemplate();
        }
        else
        {
            Document = CreateShipTemplate();
            Save();
        }

        IsDirty = false;
        SceneRevision = 0;
    }

    public void Save()
    {
        Directory.CreateDirectory(_settings.WorkspacePath);
        var json = JsonSerializer.Serialize(Document, _json);
        WriteAllTextAtomic(_settings.DocumentPath, json);
        IsDirty = false;
    }

    public void MarkDirty() => IsDirty = true;

    public void BumpSceneGeometry() => SceneRevision++;

    public static ConceptDocument CreateShipTemplate()
    {
        var doc = new ConceptDocument { Name = "Concept ship" };
        doc.Parts.Add(new ConceptPartRecord
        {
            Name = "Main hull",
            Kind = "box",
            Center = [0f, 1f, 0f],
            HalfExtents = [2f, 1f, 6f],
            Material = "hull",
            Color = [0.45f, 0.48f, 0.52f],
        });
        doc.Parts.Add(new ConceptPartRecord
        {
            Name = "Bridge",
            Kind = "box",
            Center = [0f, 2.5f, -1f],
            HalfExtents = [1.2f, 0.8f, 1.5f],
            Material = "hulldark",
            Color = [0.32f, 0.34f, 0.38f],
        });
        doc.Parts.Add(new ConceptPartRecord
        {
            Name = "Port nacelle",
            Kind = "cylinder",
            Center = [-3f, 1f, 2f],
            Radius = 0.6f,
            Height = 2.5f,
            RotationY = 90f,
            Material = "metal",
            Color = [0.75f, 0.78f, 0.82f],
        });
        doc.Parts.Add(new ConceptPartRecord
        {
            Name = "Starboard nacelle",
            Kind = "cylinder",
            Center = [3f, 1f, 2f],
            Radius = 0.6f,
            Height = 2.5f,
            RotationY = 90f,
            Material = "metal",
            Color = [0.75f, 0.78f, 0.82f],
        });
        doc.Parts.Add(new ConceptPartRecord
        {
            Name = "Engine glow",
            Kind = "sphere",
            Center = [0f, 1f, 6.5f],
            Radius = 0.45f,
            Material = "engineglow",
            Color = [1f, 0.55f, 0.2f],
        });
        doc.Camera = new OrbitCameraState { Yaw = 0.75f, Pitch = 0.28f, Distance = 28f, Target = [0f, 1f, 0f] };
        return doc;
    }

    private static void WriteAllTextAtomic(string path, string contents)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temp, contents);
            if (File.Exists(path))
                File.Delete(path);
            File.Move(temp, path);
        }
        finally
        {
            if (File.Exists(temp))
            {
                try { File.Delete(temp); }
                catch (IOException) { }
            }
        }
    }
}
