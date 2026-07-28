using Novolis.Economy.Core;
using Novolis.Economy.Core.Finance;
using Novolis.Economy.Core.Holdings;
using Novolis.Economy.Core.Transport;
using SinsOfACapitalismTycoon.Sim;

namespace SinsOfACapitalismTycoon.Sim.Policies;

/// <summary>
/// Factory buys ore at mine and ships only when liquid funds, mine stock, and logistics residual allow.
/// </summary>
internal sealed class OreHaulPolicy(
    PolicyCounters counters,
    decimal oreUnitPrice,
    decimal targetBuffer,
    decimal maxHaulPerPeriod,
    decimal wageCashReserve) : IHostPolicy
{
    public EconomyState ApplyIntents(EconomyState state, SeedIds ids, int periodIndex, int totalPeriods)
    {
        var factoryOre = HoldingLedger.GetQuantity(state, ids.FactoryFirm, ids.FactoryRegion, ids.OreId);
        var inFlight = state.Transfers
            .Where(t => t.Destination.Equals(ids.FactoryRegion) && t.ResourceId.Equals(ids.OreId))
            .Sum(t => t.Quantity);

        var need = targetBuffer - factoryOre - inFlight;
        if (need < 2m)
            return state;

        if (!state.Regions.TryGetValue(ids.MineRegion, out var mineRegion))
            return state;

        var laneKey = TransferEngine.LaneKey(ids.MineRegion, ids.FactoryRegion);
        if (!state.Lanes.TryGetValue(laneKey, out var lane))
            return state;

        var logisticsResidual = RegionCapacity.RemainingLogistics(state, mineRegion);
        var mineOre = HoldingLedger.GetQuantity(state, ids.MinerFirm, ids.MineRegion, ids.OreId);
        var qty = Math.Min(need, Math.Min(mineOre, Math.Min(maxHaulPerPeriod, Math.Min(logisticsResidual, lane.CapacityPerPeriod))));
        if (qty < 2m)
            return state;

        var liquid = state.Entities[ids.FactoryFirm].Cash.Amount
                     + DepositLedger.TotalFor(state, ids.FactoryFirm).Amount;
        var spendable = liquid - wageCashReserve;
        if (spendable < 2m * oreUnitPrice)
            return state;

        var maxAffordable = Math.Floor(spendable / oreUnitPrice);
        qty = Math.Min(qty, maxAffordable);
        if (qty < 2m)
            return state;

        var cost = Money.From(qty * oreUnitPrice);

        try
        {
            var next = HoldingLedger.TransferOwnership(
                state, ids.MinerFirm, ids.FactoryFirm, ids.MineRegion, ids.OreId, qty);
            if (!TryPay(ref next, ids.FactoryFirm, ids.MinerFirm, cost))
                return state;
            var before = next.Transfers.Count;
            next = TransferEngine.StartTransfer(
                next, ids.FactoryFirm, ids.OreId, qty, ids.MineRegion, ids.FactoryRegion);
            if (next.Transfers.Count > before)
                counters.TransfersStarted++;
            state = next;
        }
        catch (InvalidOperationException)
        {
            // skip haul this period
        }

        return state;
    }

    private static bool TryPay(ref EconomyState state, LegalEntityId debtor, LegalEntityId creditor, Money amount)
    {
        if (CashLedger.TryDebit(ref state, debtor, amount))
        {
            state = CashLedger.Credit(state, creditor, amount);
            return true;
        }

        return DepositLedger.TryPayFromDeposits(ref state, debtor, creditor, amount);
    }
}
