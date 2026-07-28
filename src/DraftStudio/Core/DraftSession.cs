using System.Text.Json;
using System.Text.Json.Serialization;
using DraftStudio.Models;

namespace DraftStudio.Core;

internal sealed class DraftSession
{
    private readonly DraftSettingsStore _settings;
    private readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public DraftSession(DraftSettingsStore settings) => _settings = settings;

    public CadDocument Document { get; private set; } = new();

    public bool IsDirty { get; private set; }

    public Guid? SelectedId { get; set; }

    public string WorkspacePath => _settings.WorkspacePath;

    public string DocumentPath => _settings.DocumentPath;

    public event Action? Changed;

    public void OpenOrCreateDefault()
    {
        _settings.Load();
        Directory.CreateDirectory(_settings.WorkspacePath);
        var path = _settings.DocumentPath;
        var legacy = Path.Combine(_settings.WorkspacePath, "draft.json");
        if (File.Exists(path))
        {
            Document = JsonSerializer.Deserialize<CadDocument>(File.ReadAllText(path), _json) ?? CreateStarter();
        }
        else if (File.Exists(legacy))
        {
            // Prefer new path; leave legacy file in place.
            Document = CreateStarter();
            Save();
        }
        else
        {
            Document = CreateStarter();
            Save();
        }

        CadVec.EnsureDefaultLayer(Document);
        IsDirty = false;
        Notify();
    }

    public void Save()
    {
        Directory.CreateDirectory(_settings.WorkspacePath);
        Document.Format = "novolis.cad";
        Document.SchemaVersion = 1;
        Document.ModifiedAt = DateTime.UtcNow.ToString("O");
        Document.CreatedAt ??= Document.ModifiedAt;
        Document.Generator = new CadGenerator { Name = "DraftStudio", Version = "2026.1.0" };
        CadVec.EnsureDefaultLayer(Document);
        File.WriteAllText(_settings.DocumentPath, JsonSerializer.Serialize(Document, _json));
        IsDirty = false;
        Notify();
    }

    public void MarkDirty()
    {
        IsDirty = true;
        Notify();
    }

    public void Notify() => Changed?.Invoke();

    public CadEntity? SelectedEntity =>
        SelectedId is { } id ? Document.Entities.FirstOrDefault(e => e.Id == id) : null;

    public static CadDocument CreateStarter()
    {
        var layerId = Guid.Parse("a0000000-0000-4000-8000-000000000001");
        var now = DateTime.UtcNow.ToString("O");
        var doc = new CadDocument
        {
            Name = "Starter sketch",
            CreatedAt = now,
            ModifiedAt = now,
            Layers =
            [
                new CadLayer
                {
                    Id = layerId,
                    Name = "0",
                    Visible = true,
                    Color = [0.8f, 0.8f, 0.8f],
                },
            ],
        };

        doc.Entities.Add(new CadEntity
        {
            Name = "Baseline",
            Kind = "line",
            LayerId = layerId,
            A = CadVec.Xz(0, -2),
            B = CadVec.Xz(0, 2),
            Color = [0.7f, 0.75f, 0.9f],
            Style = new CadStyle { Linetype = "Continuous", LineWeightMm = 0.25f },
        });
        doc.Entities.Add(new CadEntity
        {
            Name = "Origin circle",
            Kind = "circle",
            LayerId = layerId,
            Center = CadVec.Xz(0, 0),
            Radius = 1f,
            Normal = [0f, 1f, 0f],
            Color = [0.55f, 0.8f, 0.7f],
        });
        return doc;
    }
}
