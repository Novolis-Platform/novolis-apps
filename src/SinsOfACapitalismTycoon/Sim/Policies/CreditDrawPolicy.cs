using Novolis.Economy.Core;
using Novolis.Economy.Core.Finance;
using SinsOfACapitalismTycoon.Sim;

namespace SinsOfACapitalismTycoon.Sim.Policies;

/// <summary>
/// Draw committed facility when factory cash+deposits (excluding undrawn) sit below due obligations
/// or a scenario operating-cash floor — undrawn is the remedy, not the cushion.
/// </summary>
internal sealed class CreditDrawPolicy(PolicyCounters counters, Money drawAmount, Money minCashFloor) : IHostPolicy
{
    public EconomyState ApplyIntents(EconomyState state, SeedIds ids, int periodIndex, int totalPeriods)
    {
        if (!state.CreditFacilities.TryGetValue(ids.FactoryFacilityId, out var facility))
            return state;
        if (!facility.IsCommitted || facility.Available.Amount < 1m)
            return state;

        var liq = Liquidity.Of(state, ids.FactoryFirm);
        var liquid = liq.Cash.Amount + liq.AccessibleDeposits.Amount;
        var floor = Math.Max(liq.DueNow.Amount, minCashFloor.Amount);
        if (liquid + 1e-12m >= floor)
            return state;

        var need = Money.From(Math.Min(
            facility.Available.Amount,
            Math.Max(drawAmount.Amount, floor - liquid)));
        if (need.Amount < 1m)
            return state;

        try
        {
            var beforeCreated = state.Flows.MoneyCreated.Amount;
            state = CreditEngine.DrawFacility(
                state,
                ids.FactoryFacilityId,
                need,
                interestRatePerPeriod: 0.04m,
                termPeriods: 12);
            counters.CreditDraws++;
            counters.MoneyCreatedByPolicy += state.Flows.MoneyCreated.Amount - beforeCreated;
        }
        catch (InvalidOperationException)
        {
            // skip
        }

        return state;
    }
}
