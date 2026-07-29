using Novolis.Economy;
using Novolis.Economy.Accounting;
using Novolis.Economy.Logistics;
using Novolis.Economy.Simulation;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>Shared ledger posts for hull premium (accrual) and FTL overhaul remits.</summary>
internal static class HullFinance
{
  public const string PremiumMemo = "Hull insurance premium";
  public const string PremiumAccrualMemo = "Hull insurance accrual";
  public const string PremiumSettleMemo = "Hull insurance settlement";
  public const string IdleStandingMemo = "Hull idle standing";
  public const string OverhaulMemo = "FTL drive overhaul";

  /// <summary>
  /// Accrue today's category premium (expense → payable). No cash movement — like wage accrual.
  /// </summary>
  public static void AccruePremium(
    FirmLedger firm,
    ShipRegistryEntry entry,
    Money amount,
    SimulationDate day,
    string memo = PremiumAccrualMemo)
  {
    if (amount.Amount <= 0m)
    {
      return;
    }

    firm.Post(AccountRole.TransportTollExpense, AccountRole.AccountsPayable, amount, day, memo);
    entry.PremiumPayable += amount.Amount;
  }

  /// <summary>
  /// Settle accrued premium from cash to underwriter (payable → cash). Like paying wages.
  /// </summary>
  public static bool TrySettlePremium(
    FirmLedger firm,
    FirmLedger underwriter,
    ShipRegistryEntry entry,
    SimulationDate day,
    Money? maxPay = null,
    string memo = PremiumSettleMemo)
  {
    var outstanding = entry.PremiumPayable;
    if (outstanding <= 0.0001m)
    {
      entry.PremiumPayable = 0m;
      entry.PremiumArrearsDays = 0;
      return true;
    }

    var cap = maxPay is { Amount: > 0m } m ? m.Amount : outstanding;
    var payAmt = Math.Min(outstanding, Math.Min(cap, firm.Cash.Amount));
    if (payAmt <= 0.0001m)
    {
      return false;
    }

    var pay = Money.From(payAmt);
    firm.Post(AccountRole.AccountsPayable, AccountRole.Cash, pay, day, memo);
    underwriter.Post(AccountRole.Cash, AccountRole.Revenue, pay, day, memo);
    entry.PremiumPayable = Math.Max(0m, entry.PremiumPayable - payAmt);
    entry.PremiumPaid += payAmt;
    if (entry.PremiumPayable <= 0.0001m)
    {
      entry.PremiumPayable = 0m;
      entry.PremiumArrearsDays = 0;
      entry.Insured = true;
      entry.Suspended = false;
    }

    return true;
  }

  /// <summary>
  /// Legacy name: settle accrued premium (and optionally accrue nothing). Prefer
  /// <see cref="TrySettlePremium"/> + <see cref="AccruePremium"/>.
  /// </summary>
  public static bool TryRemitPremium(
    FirmLedger firm,
    FirmLedger underwriter,
    ShipRegistryEntry entry,
    Money amount,
    SimulationDate day,
    string memo = PremiumMemo)
  {
    _ = amount;
    _ = memo;
    return TrySettlePremium(firm, underwriter, entry, day);
  }

  /// <summary>
  /// Pay overhaul bill and apply registry overhaul. Returns false if cash short of <paramref name="bill"/> + reserve.
  /// </summary>
  public static bool TryPayOverhaul(
    FirmLedger firm,
    FirmLedger underwriter,
    ShipRegistry registry,
    ShipRegistryEntry entry,
    Money bill,
    SimulationDate day,
    decimal cashReserve = 40m)
  {
    if (bill.Amount <= 0m || firm.Cash.Amount < bill.Amount + cashReserve)
    {
      return false;
    }

    firm.Post(AccountRole.TransportTollExpense, AccountRole.Cash, bill, day, OverhaulMemo);
    underwriter.Post(AccountRole.Cash, AccountRole.Revenue, bill, day, OverhaulMemo);
    entry.MaintenancePaid += bill.Amount;
    registry.ApplyOverhaul(entry);
    return true;
  }
}
