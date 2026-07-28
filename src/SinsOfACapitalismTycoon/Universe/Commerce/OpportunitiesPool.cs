using Novolis.Economy;
using Novolis.Economy.Accounting;
using Novolis.Economy.Logistics;
using Novolis.Economy.Simulation;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>
/// Meridian Opportunities Registry — recurring ugly standby money.
/// Refusal / missed window ≠ premium hit (only <c>standby-pass</c> milestone).
/// </summary>
internal sealed class OpportunitiesPool
{
  private readonly CampaignWorld.Ids _ids;
  private readonly MilestoneLog _milestones;
  private readonly ReputationLedger _reputation;
  private readonly HashSet<int> _offeredDays = [];
  private FirmId? _standbyTramp;
  private decimal _standbyBonusLeft;
  private int _standbyUntilDay = -1;
  private bool _completionPaid;

  public OpportunitiesPool(
    CampaignWorld.Ids ids,
    MilestoneLog milestones,
    ReputationLedger reputation)
  {
    _ids = ids;
    _milestones = milestones;
    _reputation = reputation;
  }

  public FirmId? ActiveStandbyTramp => _standbyTramp;

  public FirmId? PreferTramp { get; set; }

  /// <summary>Player refuse: clears window without actuarial spike (standby-pass).</summary>
  public bool TryRefuse(FirmId firm, int day)
  {
    if (_standbyTramp is not { } tramp || !tramp.Equals(firm))
    {
      return false;
    }

    var name = _ids.Registry.TryGet(tramp)?.RegistryName ?? "tramp";
    _milestones.Add(day, "standby-pass",
      $"{name} refused — refusal ≠ premium hit");
    _standbyTramp = null;
    _standbyBonusLeft = 0m;
    _standbyUntilDay = -1;
    _completionPaid = false;
    return true;
  }

  public void TickHour(EconomySimulation sim, ulong seed)
  {
    var day = sim.State.Clock.Date.DayIndex;
    var hourOfDay = (int)(sim.State.Clock.HourIndex % SimulationHour.HoursPerDay);

    TickCompletion(sim, day);

    if (hourOfDay != 12)
    {
      return;
    }

    // First life-moment: day 18 (day 12 when a preferred player tramp is set).
    if (PreferTramp is not null && day == 12 && _offeredDays.Add(12))
    {
      Offer(sim, day, force: true);
      return;
    }

    if (day == 18 && _offeredDays.Add(18))
    {
      Offer(sim, day, force: true);
      return;
    }

    if (day < 50 || !_offeredDays.Add(day))
    {
      return;
    }

    var span = 35 + (int)((seed ^ (ulong)day) % 11);
    var lastOffer = _offeredDays.Where(d => d != day).DefaultIfEmpty(18).Max();
    if (day - lastOffer < span)
    {
      _offeredDays.Remove(day);
      return;
    }

    // Seed gate: ~40% of eligible days actually post.
    if (((seed ^ (ulong)(day * 7919)) & 0xFF) > 100)
    {
      _offeredDays.Remove(day);
      return;
    }

    Offer(sim, day, force: false);
  }

  public void TickDayEnd(EconomySimulation sim, int day)
  {
    if (_standbyTramp is null || day <= _standbyUntilDay)
    {
      return;
    }

    if (!_completionPaid)
    {
      var name = _ids.Registry.TryGet(_standbyTramp.Value)?.RegistryName ?? "tramp";
      _milestones.Add(day, "standby-pass",
        $"{name} window closed — refusal ≠ premium hit");
    }

    _standbyTramp = null;
    _standbyBonusLeft = 0m;
    _standbyUntilDay = -1;
    _completionPaid = false;
  }

  private void Offer(EconomySimulation sim, int day, bool force)
  {
    FirmId? tramp = null;
    if (PreferTramp is { } prefer && _ids.Registry.CanOperate(prefer))
    {
      tramp = prefer;
    }

    tramp ??= _reputation.PreferOperable(
      _ids.Carriers.Concat(_ids.Registry.Entries.Where(e => e.OwnerMaster).Select(e => e.FirmId)).Distinct(),
      _ids.Registry.CanOperate);
    if (tramp is null || tramp.Value.Value == Guid.Empty)
    {
      tramp = _ids.Carriers.FirstOrDefault(f => _ids.Registry.CanOperate(f));
    }

    if (tramp is null || tramp.Value.Value == Guid.Empty)
    {
      tramp = _ids.Carrier;
    }

    if (!sim.State.World.Ledgers.TryGetValue(_ids.Station, out var station)
        || !sim.State.World.Ledgers.TryGetValue(tramp.Value, out var firm)
        || station.Cash.Amount < 800m)
    {
      if (force)
      {
        _milestones.Add(day, "standby-pass", "no operable tramp for ugly money");
      }

      return;
    }

    var bonus = Money.From(Math.Min(1_200m, station.Cash.Amount * 0.06m));
    station.Post(AccountRole.WageExpense, AccountRole.Cash, bonus, sim.State.Clock.Date,
      "Ugly standby retainer");
    firm.Post(AccountRole.Cash, AccountRole.Revenue, bonus, sim.State.Clock.Date,
      "Ugly standby retainer");
    _standbyTramp = tramp;
    _standbyBonusLeft = bonus.Amount * 0.5m;
    _standbyUntilDay = day + 14;
    _completionPaid = false;
    var name = _ids.Registry.TryGet(tramp.Value)?.RegistryName ?? "tramp";
    _milestones.Add(day, "ugly-standby",
      $"{name} retainer {bonus.Amount:0} — completion crews, not heroes");
  }

  private void TickCompletion(EconomySimulation sim, int day)
  {
    if (_standbyTramp is not { } tramp || _standbyBonusLeft <= 0m || day > _standbyUntilDay
        || _completionPaid)
    {
      return;
    }

    foreach (var ev in sim.State.Events.TakeLast(24))
    {
      if (ev is not ShipmentDelivered d || !d.FirmId.Equals(tramp))
      {
        continue;
      }

      if (!sim.State.World.Ledgers.TryGetValue(_ids.Station, out var station)
          || !sim.State.World.Ledgers.TryGetValue(tramp, out var firm)
          || station.Cash.Amount + 0.0001m < _standbyBonusLeft)
      {
        _standbyBonusLeft = 0m;
        return;
      }

      var kick = Money.From(_standbyBonusLeft);
      station.Post(AccountRole.WageExpense, AccountRole.Cash, kick, sim.State.Clock.Date,
        "Ugly standby completion kicker");
      firm.Post(AccountRole.Cash, AccountRole.Revenue, kick, sim.State.Clock.Date,
        "Ugly standby completion kicker");
      var name = _ids.Registry.TryGet(tramp)?.RegistryName ?? "tramp";
      _milestones.Add(day, "known-responsive",
        $"{name} finished standby — listed → known responsive (+{kick.Amount:0})");
      _reputation.ObserveStandbyComplete(tramp);
      _reputation.ObserveKnownResponsive(tramp);
      _standbyBonusLeft = 0m;
      _completionPaid = true;
      return;
    }
  }
}
