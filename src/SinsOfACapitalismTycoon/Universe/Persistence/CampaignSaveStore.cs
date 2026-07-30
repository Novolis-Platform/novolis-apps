using Microsoft.Extensions.DependencyInjection;
using Novolis.Storage.Abstractions;
using Novolis.Storage.Json;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>
/// Campaign checkpoints on disk via <see cref="Novolis.Storage.Json"/> repositories.
/// Root: %LocalAppData%/Novolis/SinsOfACapitalismTycoon/saves
/// </summary>
internal sealed class CampaignSaveStore
{
  private readonly IRepository<CampaignSaveRecord> _repo;
  private readonly ServiceProvider _provider;

  public CampaignSaveStore(string? rootPath = null)
  {
    RootPath = string.IsNullOrWhiteSpace(rootPath)
      ? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Novolis",
        "SinsOfACapitalismTycoon",
        "saves")
      : Path.GetFullPath(rootPath);

    Directory.CreateDirectory(RootPath);

    var services = new ServiceCollection();
    services.AddStorage(b => b.AddJsonProvider(o =>
    {
      o.RootPath = RootPath;
      o.UseProcessLock = false;
    }));
    _provider = services.BuildServiceProvider();
    _repo = _provider.GetRequiredService<IRepository<CampaignSaveRecord>>();
  }

  public string RootPath { get; }

  public static CampaignSaveStore Default => _lazy.Value;

  private static readonly Lazy<CampaignSaveStore> _lazy = new(() => new CampaignSaveStore());

  public IReadOnlyList<CampaignSaveRecord> List() =>
    _repo.All().OrderByDescending(s => s.SavedUtc).ToList();

  public ValueTask<CampaignSaveRecord?> TryGetAsync(Guid id, CancellationToken ct = default) =>
    _repo.TryGetAsync(id, ct);

  public CampaignSaveRecord? TryGetLatest() => List().FirstOrDefault();

  public async ValueTask<CampaignSaveRecord> SaveAsync(
    CampaignRunner.LiveSession session,
    string? label = null,
    CancellationToken ct = default)
  {
    var snap = TrampSurvival.Capture(session.Ids);
    var entry = session.Ids.Registry.TryGet(session.Ids.Carrier);
    var cash = session.Sim.State.World.Ledgers.TryGetValue(session.Ids.Carrier, out var ledger)
      ? ledger.Cash.Amount
      : 0m;

    var record = new CampaignSaveRecord
    {
      Id = Guid.CreateVersion7(),
      SchemaVersion = CampaignSaveRecord.CurrentSchema,
      Label = string.IsNullOrWhiteSpace(label)
        ? $"d{session.Sim.State.Clock.Date.DayIndex} · seed {session.Sim.State.Seed}"
        : label.Trim(),
      SavedUtc = DateTimeOffset.UtcNow,
      Seed = session.Sim.State.Seed,
      HorizonHours = session.RequestedHours,
      HoursDone = session.HoursDone,
      Drama = session.DramaEnabled,
      LastTramp = session.Player.LastTrampMode,
      Autopilot = session.Player.Autopilot,
      Player = session.Player.Enabled,
      DockBoardOnly = session.Player.DockBoardOnly,
      MeshBoardUnlocked = session.Player.MeshBoardUnlocked,
      LastTrampWon = session.Player.LastTrampWon,
      LastTrampLost = session.Player.LastTrampLost,
      DayIndex = session.Sim.State.Clock.Date.DayIndex,
      HubSystemId = session.CurrentHubSystemId,
      SurvivalLine = TrampSurvival.FormatLine(
        snap, session.Player.LastTrampMode, session.Player.LastTrampWon, session.Player.LastTrampLost,
        session.Sim.State.Clock.Date.DayIndex, session.Ids),
      StandingLine = entry?.StandingLabel ?? "—",
      OpsCash = cash,
      SimHash = session.Sim.State.Hash,
    };

    await _repo.UpsertAsync(record, ct).ConfigureAwait(false);
    return record;
  }

  public ValueTask<bool> DeleteAsync(Guid id, CancellationToken ct = default) =>
    _repo.DeleteAsync(id, ct);
}
