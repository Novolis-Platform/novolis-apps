using Novolis.Hours.Domain;
using Novolis.Hours.Domain.Dimensions;
using Novolis.Hours.Domain.Ledger;
using Novolis.Hours.Domain.Work;

namespace Novolis.Hours.Application;

/// <summary>Atomic application boundary for a work fact and its ledger projection.</summary>
public sealed class WorkLedgerService
{
    private readonly IHoursJournalBatch journal;

    /// <summary>Initializes the service over a batch-capable journal.</summary>
    public WorkLedgerService(IHoursJournalBatch journal)
    {
        this.journal = journal ?? throw new ArgumentNullException(nameof(journal));
    }

    /// <summary>
    /// Appends the immutable registration and balanced derived ledger transaction
    /// as one authoritative journal batch.
    /// </summary>
    public async ValueTask<LedgerTransaction> RecordAsync(
        WorkRegistration registration,
        DimensionEvaluation evaluation,
        LedgerProjector projector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(evaluation);
        ArgumentNullException.ThrowIfNull(projector);
        if (evaluation.WorkDay.EffectiveRegistration.Id != registration.Id)
        {
            throw new ArgumentException(
                "The Dimension evaluation must be based on the supplied registration.",
                nameof(evaluation));
        }

        var transaction = projector.Project(evaluation);
        var registrationEvent = HoursEvent.Create(
            registration.WorkDay.EmployeeId,
            HoursEventType.WorkRegistrationRecorded,
            registration,
            registration.RecordedBy.ToHoursActor(),
            registration.RecordedAt);
        var ledgerEvent = HoursEvent.Create(
            registration.WorkDay.EmployeeId,
            HoursEventType.LedgerTransactionRecorded,
            transaction,
            registration.RecordedBy.ToHoursActor(),
            registration.RecordedAt);
        await journal.AppendBatchAsync(
            [registrationEvent, ledgerEvent],
            cancellationToken).ConfigureAwait(false);
        return transaction;
    }
}
