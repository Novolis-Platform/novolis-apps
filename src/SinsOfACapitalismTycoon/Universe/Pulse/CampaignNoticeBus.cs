using Novolis.Economy;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>Session-scoped typed notice queue (escrow, soft-fail, grounding) for mesh / UI drains.</summary>
internal sealed class CampaignNoticeBus
{
  public readonly record struct Notice(
    string Channel,
    string Kind,
    string Detail,
    int Day,
    FirmId? SubjectFirm = null,
    string? RegistryName = null,
    decimal Amount = 0m);

  private readonly List<Notice> _pending = [];

  public void Publish(Notice notice) => _pending.Add(notice);

  public void Publish(
    string channel,
    string kind,
    string detail,
    int day,
    FirmId? subjectFirm = null,
    string? registryName = null,
    decimal amount = 0m) =>
    Publish(new Notice(channel, kind, detail, day, subjectFirm, registryName, amount));

  public IReadOnlyList<Notice> Drain()
  {
    if (_pending.Count == 0)
    {
      return Array.Empty<Notice>();
    }

    var copy = _pending.ToList();
    _pending.Clear();
    return copy;
  }

  public IReadOnlyList<Notice> DrainChannel(string channel)
  {
    if (_pending.Count == 0)
    {
      return Array.Empty<Notice>();
    }

    var hit = _pending.Where(n => n.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase)).ToList();
    if (hit.Count == 0)
    {
      return Array.Empty<Notice>();
    }

    _pending.RemoveAll(n => n.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase));
    return hit;
  }
}
