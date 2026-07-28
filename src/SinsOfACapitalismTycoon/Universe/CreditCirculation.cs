using Novolis.Economy;
using Novolis.Economy.Accounting;
using Novolis.Economy.Core;
using Novolis.Economy.Core.Extensions;
using Novolis.Economy.Finance;
using Novolis.Economy.Production;
using Novolis.Economy.Simulation;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>Point-in-time macro snapshot for milestone comparison.</summary>
internal readonly record struct MacroSnapshot(
  long HourIndex,
  int DayIndex,
  decimal Liquid,
  decimal Households,
  decimal FirmCash,
  decimal InventoryBook,
  decimal Produced,
  decimal RetailSold,
  int BookFills,
  decimal Delivered,
  int Departed,
  int LoansDefaulted,
  decimal DividendsPaid,
  int FacilitiesAbsorbed,
  int Upgrades,
  decimal SkuRaw,
  decimal SkuCapital,
  decimal SkuFinal,
  decimal SkuEnergy,
  int CorePeriod,
  decimal CoreCash,
  decimal CoreHoldingQty,
  int CoreHoldingSlots,
  int CoreInFlight);

/// <summary>Dashboard metrics for liquid stock, imports, finance, macro events, and activity.</summary>
internal sealed class CreditCirculation
{
  private readonly EconomySimulation _sim;
  private int _eventCursor;
  private decimal _wagesDistributed;
  private decimal _importSpend;
  private decimal _exportRevenue;
  private decimal _exportQty;
  private int _exportFills;
  private int _trampVentures;
  private decimal _tollsToTreasury;
  private decimal _produced;
  private decimal _retailSold;
  private int _bookFills;
  private decimal _bookFillQty;
  private int _departed;
  private decimal _interestAccrued;
  private decimal _interestPaid;
  private int _loansOriginated;
  private int _loansDefaulted;
  private int _dividends;
  private decimal _dividendCash;
  private int _ownershipChanges;
  private int _creditFreezes;
  private int _facilitiesAbsorbed;
  private int _facilityUpgrades;
  private int _facilityUpgradeFails;
  private int _b2bFills;
  private decimal _b2bQty;
  private int _b2bFailCash;
  private int _b2bFailStock;
  private readonly Dictionary<string, int> _planFailReasons = new(StringComparer.Ordinal);
  private readonly List<string> _macroLog = [];
  private readonly List<MacroSnapshot> _milestones = [];
  private int _lastMilestoneDay = -1;
  private Dictionary<FirmId, string> _firmNames = new();
  private ProductId? _ore;
  private ProductId? _parts;
  private ProductId? _goods;
  private ProductId? _fuel;

  public CreditCirculation(EconomySimulation sim)
  {
    _sim = sim;
    _eventCursor = sim.State.Events.Count;
  }

  /// <summary>Optional display names for macro event log.</summary>
  public void SetFirmNames(IEnumerable<(string Name, FirmId Id)> firms)
  {
    _firmNames = firms.ToDictionary(f => f.Id, f => f.Name);
  }

  /// <summary>SKU ids for inventory-by-product milestones.</summary>
  public void SetSkuIds(ProductId ore, ProductId parts, ProductId goods, ProductId fuel)
  {
    _ore = ore;
    _parts = parts;
    _goods = goods;
    _fuel = fuel;
  }

  public decimal WagesDistributed => _wagesDistributed;
  public decimal ImportSpend => _importSpend;
  public decimal ExportRevenue => _exportRevenue;
  public decimal ExportQty => _exportQty;
  public int ExportFills => _exportFills;
  public int TrampVentures => _trampVentures;
  public decimal TollsToTreasury => _tollsToTreasury;
  public decimal Produced => _produced;
  public decimal RetailSold => _retailSold;
  public int BookFills => _bookFills;
  public decimal BookFillQty => _bookFillQty;
  public int Departed => _departed;
  public decimal InterestAccrued => _interestAccrued;
  public decimal InterestPaid => _interestPaid;
  public int LoansOriginated => _loansOriginated;
  public int LoansDefaulted => _loansDefaulted;
  public int Dividends => _dividends;
  public decimal DividendCash => _dividendCash;
  public int OwnershipChanges => _ownershipChanges;
  public int CreditFreezes => _creditFreezes;
  public int FacilitiesAbsorbed => _facilitiesAbsorbed;
  public int FacilityUpgrades => _facilityUpgrades;
  public int FacilityUpgradeFails => _facilityUpgradeFails;
  public int B2bFills => _b2bFills;
  public decimal B2bQty => _b2bQty;
  public int B2bFailCash => _b2bFailCash;
  public int B2bFailStock => _b2bFailStock;
  public IReadOnlyDictionary<string, int> PlanFailReasons => _planFailReasons;
  public IReadOnlyList<string> MacroLog => _macroLog;
  public IReadOnlyList<MacroSnapshot> Milestones => _milestones;

  public decimal LiquidStock => MoneyStock.Liquid(_sim.State.World);

  public int ActiveLoans =>
    _sim.State.World.Loans.Count(l => l.Status == Novolis.Economy.Finance.LoanStatus.Active);

  public decimal PrincipalOutstanding =>
    _sim.State.World.Loans
      .Where(l => l.Status is Novolis.Economy.Finance.LoanStatus.Active
        or Novolis.Economy.Finance.LoanStatus.Defaulted)
      .Sum(l => l.PrincipalRemaining.Amount);

  public int CreditFrozenFirms =>
    _sim.State.World.Entities.Values.Count(e => e.CreditFrozen);

  public decimal InventoryBookValue
  {
    get
    {
      var inv = _sim.State.World.Inventory;
      var sum = 0m;
      foreach (var key in inv.Keys)
      {
        foreach (var lot in inv.GetLots(key))
        {
          sum += lot.Quantity.Value * lot.UnitCost.Amount;
        }
      }

      return sum;
    }
  }

  public void ObserveAfterPulse(int eventsBeforePulse)
  {
    var events = _sim.State.Events;
    var clock = _sim.State.Clock;
    for (var i = Math.Max(eventsBeforePulse, _eventCursor); i < events.Count; i++)
    {
      switch (events[i])
      {
        case HouseholdCreditsIssued e:
          _wagesDistributed += e.Amount.Amount;
          break;
        case ProcurementFilled e:
          _importSpend += e.UnitPrice.Amount * e.Quantity.Value;
          break;
        case ExportFilled e:
          _exportFills++;
          _exportQty += e.Quantity.Value;
          _exportRevenue += e.Revenue.Amount;
          break;
        case TransportTollPaid e:
          _tollsToTreasury += e.Amount.Amount;
          break;
        case BatchProduced e:
          _produced += e.Quantity.Value;
          break;
        case GoodsSold e:
          _retailSold += e.Quantity.Value;
          break;
        case GoodsSoldInterFirm e:
          _b2bFills++;
          _b2bQty += e.Quantity.Value;
          break;
        case TransferGoodsFailed e:
          if (string.Equals(e.Reason, "cash", StringComparison.OrdinalIgnoreCase))
          {
            _b2bFailCash++;
          }
          else
          {
            _b2bFailStock++;
          }

          break;
        case HubOrderFilled e:
          _bookFills++;
          _bookFillQty += e.Quantity.Value;
          break;
        case ShipmentDeparted:
          _departed++;
          break;
        case ShipmentPlanFailed e:
          _planFailReasons[e.Reason] = _planFailReasons.GetValueOrDefault(e.Reason) + 1;
          break;
        case InterestAccrued e:
          _interestAccrued += e.Amount.Amount;
          break;
        case LoanRepaid e:
          _interestPaid += e.Amount.Amount;
          break;
        case LoanOriginated e:
          _loansOriginated++;
          Note(clock, $"loan {e.Principal.Amount:0} {Short(e.LenderFirmId)}→{Short(e.BorrowerFirmId)}");
          // Hull loans to venture tramp ids (…00c0+)
          if (e.BorrowerFirmId.Value.ToString("N").Contains("00000000c0", StringComparison.Ordinal))
          {
            _trampVentures++;
            Note(clock, $"VENTURE hull loan {e.Principal.Amount:0}");
          }

          break;
        case LoanDefaulted e:
          _loansDefaulted++;
          Note(clock, $"DEFAULT {Short(e.BorrowerFirmId)} owe {e.PrincipalRemaining.Amount:0}");
          break;
        case DividendPaid e:
          _dividends++;
          _dividendCash += e.Amount.Amount;
          Note(clock, $"dividend {e.Amount.Amount:0} {Short(e.IssuerFirmId)}→{Short(e.OwnerFirmId)}");
          break;
        case OwnershipChanged e:
          _ownershipChanges++;
          Note(clock, $"own {e.Fraction:0.##} of {Short(e.IssuerFirmId)} → {Short(e.OwnerFirmId)}");
          break;
        case CreditFrozenSet e:
          _creditFreezes++;
          Note(clock, $"credit FROZEN {Short(e.FirmId)}");
          break;
        case FacilityAbsorbed e:
          _facilitiesAbsorbed++;
          Note(clock, $"absorb facility {Short(e.FromFirmId)}→{Short(e.ToFirmId)}");
          break;
        case FacilityUpgraded e:
          _facilityUpgrades++;
          Note(clock, $"upgrade ×{e.CapacityFactor:0.##} cost {e.Cost.Amount:0} {Short(e.OwnerFirmId)}");
          break;
        case FacilityUpgradeFailed e:
          _facilityUpgradeFails++;
          Note(clock, $"upgrade FAIL {e.Reason}");
          break;
      }
    }

    _eventCursor = events.Count;
    MaybeCaptureMilestone();
  }

  /// <summary>Force a milestone at end-of-run if the last day wasn't already captured.</summary>
  public void CaptureFinalMilestone() => CaptureMilestone(force: true);

  private void MaybeCaptureMilestone()
  {
    var day = _sim.State.Clock.Date.DayIndex;
    // Capture at 1, then every 100 days (100, 200, …).
    if (day == 1 || (day > 0 && day % 100 == 0 && day != _lastMilestoneDay))
    {
      CaptureMilestone(force: false);
    }
  }

  private void CaptureMilestone(bool force)
  {
    var day = _sim.State.Clock.Date.DayIndex;
    if (!force && day == _lastMilestoneDay)
    {
      return;
    }

    if (force && _milestones.Count > 0 && _milestones[^1].DayIndex == day)
    {
      return;
    }

    _lastMilestoneDay = day;
    var world = _sim.State.World;
    var sku = InventoryBySku();
    var core = world.CoreState.Snapshot();
    _milestones.Add(new MacroSnapshot(
      _sim.State.Clock.HourIndex,
      day,
      LiquidStock,
      world.Cohorts.Sum(c => c.BudgetRemaining.Amount),
      world.Ledgers.Values.Sum(l => l.Cash.Amount),
      InventoryBookValue,
      _produced,
      _retailSold,
      _bookFills,
      world.TransportStats.CargoDelivered.Value,
      _departed,
      _loansDefaulted,
      _dividendCash,
      _facilitiesAbsorbed,
      _facilityUpgrades,
      sku.Raw,
      sku.Capital,
      sku.Final,
      sku.Energy,
      core.Period,
      core.TotalCash.Amount,
      world.CoreState.Holdings.Values.Sum(h => h.Quantity),
      core.HoldingSlots,
      core.InFlightTransfers));
  }

  /// <summary>Physical lots by campaign SKU (ops inventory; Core holdings are a parallel credit path).</summary>
  public (decimal Raw, decimal Capital, decimal Final, decimal Energy) InventoryBySku()
  {
    var world = _sim.State.World;
    decimal raw = 0, capital = 0, final = 0, energy = 0;
    foreach (var key in world.Inventory.Keys)
    {
      var qty = world.Inventory.GetLots(key).Sum(l => l.Quantity.Value);
      if (_ore is { } o && key.ProductId.Equals(o))
      {
        raw += qty;
      }
      else if (_parts is { } p && key.ProductId.Equals(p))
      {
        capital += qty;
      }
      else if (_goods is { } g && key.ProductId.Equals(g))
      {
        final += qty;
      }
      else if (_fuel is { } f && key.ProductId.Equals(f))
      {
        energy += qty;
      }
    }

    return (raw, capital, final, energy);
  }

  private void Note(SimulationHour clock, string text)
  {
    const int max = 40;
    var line = $"d{clock.Date.DayIndex}h{clock.HourIndex % 24} {text}";
    _macroLog.Add(line);
    if (_macroLog.Count > max)
    {
      _macroLog.RemoveAt(0);
    }
  }

  private string Short(FirmId id) =>
    _firmNames.TryGetValue(id, out var name) ? name : id.Value.ToString("N")[..8];
}
