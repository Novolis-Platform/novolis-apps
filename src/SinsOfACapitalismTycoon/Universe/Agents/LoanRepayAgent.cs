using Novolis.Economy;
using Novolis.Economy.Agents;
using Novolis.Economy.Finance;
using Novolis.Economy.Production;
using Novolis.Economy.Simulation;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>Borrowers repay before due when liquid — avoids default absorb when possible.</summary>
internal sealed class LoanRepayAgent : IEconomicAgent
{
  public const decimal CashFloorAfterRepay = 800m;
  public const long RepayWithinHours = 72;

  private readonly CampaignWorld.Ids _ids;
  private readonly MilestoneLog _milestones;

  public LoanRepayAgent(CampaignWorld.Ids ids, MilestoneLog milestones)
  {
    _ids = ids;
    _milestones = milestones;
    FirmId = ids.Industry;
  }

  public FirmId FirmId { get; }

  public string LastDecision { get; private set; } = "repay idle";

  public void Tick(AgentContext context)
  {
    if (context.Clock.HourIndex % SimulationHour.HoursPerDay != 10)
    {
      return;
    }

    var world = context.World;
    var hour = context.Clock.HourIndex;
    var borrowers = new[] { _ids.Industry, _ids.Mining }
      .Concat(_ids.Carriers)
      .Append(_ids.MegaHauler)
      .OrderBy(f => f.Value);

    foreach (var borrower in borrowers)
    {
      if (!world.Ledgers.TryGetValue(borrower, out var ledger))
      {
        continue;
      }

      var loan = world.Loans
        .Where(l => l.BorrowerFirmId.Equals(borrower) && l.Status == LoanStatus.Active)
        .OrderBy(l => l.DueAt.HourIndex)
        .FirstOrDefault();
      if (loan is null)
      {
        continue;
      }

      var hoursLeft = loan.DueAt.HourIndex - hour;
      if (hoursLeft > RepayWithinHours && loan.PrincipalRemaining.Amount > 500m)
      {
        // Partial drip when flush.
        if (ledger.Cash.Amount < CashFloorAfterRepay + 400m)
        {
          continue;
        }

        var drip = Money.From(Math.Min(400m, loan.PrincipalRemaining.Amount));
        context.Enqueue(new RepayLoan(loan.Id, drip));
        LastDecision = $"drip repay {drip.Amount:0}";
        continue;
      }

      if (hoursLeft > RepayWithinHours)
      {
        continue;
      }

      var affordable = ledger.Cash.Amount - CashFloorAfterRepay;
      if (affordable < 50m)
      {
        LastDecision = "repay cannot — thin";
        continue;
      }

      var pay = Money.From(Math.Min(affordable, loan.PrincipalRemaining.Amount));
      context.Enqueue(new RepayLoan(loan.Id, pay));
      LastDecision = $"repay due-soon {pay.Amount:0}";
      if (hoursLeft <= 24)
      {
        _milestones.Add(context.Clock.Date.DayIndex, "repay",
          $"due-soon repay {pay.Amount:0} borrower {borrower.Value.ToString("N")[..8]}");
      }
    }

    // Observe defaults for milestones.
    foreach (var d in world.Loans.Where(l => l.Status == LoanStatus.Defaulted))
    {
      _milestones.AddOnce(
        context.Clock.Date.DayIndex,
        "default",
        $"loan default {d.Id.Value.ToString("N")[..8]} borrower {d.BorrowerFirmId.Value.ToString("N")[..8]}");
    }
  }
}
