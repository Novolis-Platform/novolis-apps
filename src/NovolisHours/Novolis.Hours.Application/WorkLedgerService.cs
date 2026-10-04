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

    /// <summary>Appends only the balanced transaction when the registration fact already exists.</summary>
    public async ValueTask<LedgerTransaction> AppendTransactionAsync(
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
        var ledgerEvent = HoursEvent.Create(
            registration.WorkDay.EmployeeId,
            HoursEventType.LedgerTransactionRecorded,
            transaction,
            registration.RecordedBy.ToHoursActor(),
            registration.RecordedAt);
        await journal.AppendBatchAsync([ledgerEvent], cancellationToken).ConfigureAwait(false);
        return transaction;
    }

    /// <summary>Appends only the balanced difference between an original and corrected projection.</summary>
    public async ValueTask<LedgerTransaction> AppendCorrectionTransactionAsync(
        LedgerTransaction original,
        WorkRegistration correctedRegistration,
        DimensionEvaluation correctedEvaluation,
        LedgerProjector projector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(correctedRegistration);
        ArgumentNullException.ThrowIfNull(correctedEvaluation);
        ArgumentNullException.ThrowIfNull(projector);
        var corrected = projector.Project(correctedEvaluation);
        var difference = projector.CreateCorrection(original, corrected);
        var ledgerEvent = HoursEvent.Create(
            correctedRegistration.WorkDay.EmployeeId,
            HoursEventType.LedgerTransactionRecorded,
            difference,
            correctedRegistration.RecordedBy.ToHoursActor(),
            correctedRegistration.RecordedAt);
        await journal.AppendBatchAsync([ledgerEvent], cancellationToken).ConfigureAwait(false);
        return difference;
    }
}
