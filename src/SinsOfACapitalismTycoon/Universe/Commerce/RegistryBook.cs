using Novolis.Economy;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>Typed registry book — one door per kind (ship, firm, license, …).</summary>
internal sealed class RegistryBook<T>
  where T : RegistryRecord
{
  private readonly Dictionary<Guid, T> _byId = new();

  public RegistryKind Kind { get; }

  public RegistryBook(RegistryKind kind) => Kind = kind;

  public IReadOnlyCollection<T> Entries => _byId.Values;

  public int Count => _byId.Count;

  public void Register(T entry)
  {
    if (entry.Kind != Kind)
    {
      throw new InvalidOperationException(
        $"Cannot register {entry.Kind} into {Kind} book.");
    }

    _byId[entry.SubjectId] = entry;
  }

  public T? TryGet(Guid subjectId) =>
    _byId.TryGetValue(subjectId, out var e) ? e : null;

  public bool CanAct(Guid subjectId) =>
    TryGet(subjectId) is { } e && e.CanAct;

  public void AttachLien(Guid subjectId, decimal amount)
  {
    if (TryGet(subjectId) is { } e && amount > 0m)
    {
      e.LienPrincipal += amount;
    }
  }
}
