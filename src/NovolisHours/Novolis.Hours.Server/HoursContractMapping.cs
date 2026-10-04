using Novolis.Hours.Contracts;
using DomainAdjustmentReason = Novolis.Hours.Domain.HoursAdjustmentReason;
using DomainAdjustmentResolution = Novolis.Hours.Domain.HoursAdjustmentResolution;
using DomainAdjustmentResponse = Novolis.Hours.Domain.HoursAdjustmentResponse;
using DomainActorRole = Novolis.Hours.Domain.HoursActorRole;
using DomainCadence = Novolis.Hours.Domain.FlexSettlementCadence;
using DomainCompensation = Novolis.Hours.Domain.FinancialCompensationSlice;
using DomainWorkSource = Novolis.Hours.Domain.Work.WorkRecordSource;

namespace Novolis.Hours.Server;

/// <summary>Maps Hours wire contracts onto domain types at the HTTP boundary.</summary>
internal static class HoursContractMapping
{
    public static HoursClientRole ToClient(DomainActorRole role) =>
        Enum.Parse<HoursClientRole>(role.ToString());

    public static DomainActorRole ToActor(HoursClientRole role) =>
        Enum.Parse<DomainActorRole>(role.ToString());

    public static DomainWorkSource ToDomain(WorkRecordSource source) =>
        Enum.Parse<DomainWorkSource>(source.ToString());

    public static DomainWorkSource? ToDomain(WorkRecordSource? source) =>
        source is null ? null : ToDomain(source.Value);

    public static DomainAdjustmentReason ToDomain(HoursAdjustmentReason reason) =>
        Enum.Parse<DomainAdjustmentReason>(reason.ToString());

    public static DomainAdjustmentResponse ToDomain(HoursAdjustmentResponse response) =>
        Enum.Parse<DomainAdjustmentResponse>(response.ToString());

    public static DomainAdjustmentResolution ToDomain(HoursAdjustmentResolution resolution) =>
        Enum.Parse<DomainAdjustmentResolution>(resolution.ToString());

    public static FlexSettlementCadence ToWire(DomainCadence cadence) =>
        Enum.Parse<FlexSettlementCadence>(cadence.ToString());

    public static DomainCompensation ToDomain(FinancialCompensationSlice slice) =>
        new(slice.Start, slice.End, slice.Reason);
}
