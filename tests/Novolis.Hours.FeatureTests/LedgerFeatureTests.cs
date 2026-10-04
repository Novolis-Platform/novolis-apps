using Novolis.Hours.Domain.Calendars;
using Novolis.Hours.Domain.Configuration;
using Novolis.Hours.Domain.Dimensions;
using Novolis.Hours.Domain.Ledger;
using Novolis.Hours.Domain.Work;
using Novolis.Hours.Domain;
using Novolis.Hours.Application;
using Novolis.Hours.Storage;

namespace Novolis.Hours.FeatureTests;

public sealed class LedgerFeatureTests
{
    [Test]
    public async Task Derived_flex_measure_creates_a_balanced_two_account_transaction()
    {
        var evaluation = EvaluateSignedFlex(TimeSpan.FromMinutes(45));
        var projector = new LedgerProjector(
        [
            new DimensionLedgerMapping("time-type", "flex-credit", 1),
        ]);

        var transaction = projector.Project(evaluation);

        await Assert.That(transaction.Postings.Aggregate(
                TimeSpan.Zero,
                (total, posting) => total + posting.SignedDuration))
            .IsEqualTo(TimeSpan.Zero);
        await Assert.That(transaction.Postings).Contains(posting =>
            posting.Account == DurationAccount.EmployeeFlex &&
            posting.SignedDuration == TimeSpan.FromMinutes(45));
        await Assert.That(transaction.Postings).Contains(posting =>
            posting.Account == DurationAccount.OrganisationControl &&
            posting.SignedDuration == TimeSpan.FromMinutes(-45));
    }

    [Test]
    public async Task Correction_appends_only_the_difference_and_replay_reconstructs_saldo()
    {
        var projector = new LedgerProjector(
        [
            new DimensionLedgerMapping("time-type", "flex-credit", 1),
        ]);
        var original = projector.Project(EvaluateSignedFlex(TimeSpan.FromMinutes(45)));
        var corrected = projector.Project(EvaluateSignedFlex(TimeSpan.FromMinutes(30)));
        var correction = projector.CreateCorrection(original, corrected, "Corrected flex registration");

        await Assert.That(correction.Postings).Contains(posting =>
            posting.Account == DurationAccount.EmployeeFlex &&
            posting.SignedDuration == TimeSpan.FromMinutes(-15));
        await Assert.That(new LedgerSaldoProjector().Replay([original, correction]))
            .IsEqualTo(TimeSpan.FromMinutes(30));
    }

    [Test]
    public async Task Manual_project_measure_has_no_ledger_effect_and_unbalanced_transactions_are_rejected()
    {
        var evaluation = EvaluateManualProject();
        var projector = new LedgerProjector(
        [
            new DimensionLedgerMapping("time-type", "flex-credit", 1),
        ]);
        var transaction = projector.Project(evaluation);

        await Assert.That(transaction.Postings.Aggregate(
                TimeSpan.Zero,
                (total, posting) => total + posting.SignedDuration))
            .IsEqualTo(TimeSpan.Zero);
        await Assert.That(transaction.Postings.All(posting =>
            posting.SignedDuration == TimeSpan.Zero)).IsTrue();
        await Assert.That(() => new LedgerTransaction(
            Guid.CreateVersion7(),
            evaluation.WorkDay.Key,
            evaluation.WorkDay.EffectiveRegistration.Id,
            [
                new DurationPosting(DurationAccount.EmployeeFlex, TimeSpan.FromHours(1)),
                new DurationPosting(DurationAccount.OrganisationControl, TimeSpan.Zero),
            ],
            "unbalanced")).Throws<ArgumentException>();
    }

    [Test]
    public async Task Registration_and_ledger_projection_are_appended_as_one_batch()
    {
        var evaluation = EvaluateSignedFlex(TimeSpan.FromMinutes(45));
        var journal = new InMemoryHoursJournal();
        await new WorkLedgerService(journal).RecordAsync(
            evaluation.WorkDay.EffectiveRegistration,
            evaluation,
            new LedgerProjector(
            [
                new DimensionLedgerMapping("time-type", "flex-credit", 1),
            ]));

        var events = await journal.ReadAllAsync();
        await Assert.That(events.Length).IsEqualTo(2);
        await Assert.That(events.Select(entry => entry.Type))
            .Contains(HoursEventType.LedgerTransactionRecorded);
    }

    private static DimensionEvaluation EvaluateSignedFlex(TimeSpan duration)
    {
        var resolved = CreateResolvedDay();
        var configuration = new DimensionConfiguration(
        [
            new DimensionConfigurationLayer(
                "organisation",
                "2026.1",
                10,
                [
                    new DimensionDefinition(
                        "time-type",
                        "Time type",
                        DimensionAssignmentMode.Derived,
                        DimensionCardinality.Exclusive,
                        false,
                        [new DimensionValue("flex-credit", "Flex credit")]),
                ],
                [
                    new DimensionRuleRegistration(
                        "flex",
                        10,
                        new RuleRef("flex", "1", RuleSource.Manual),
                        new SignedDurationDimensionRule(
                            "time-type",
                            "flex-credit",
                            duration)),
                ],
                RuleSource.Manual),
        ]);
        return new DimensionEvaluator().Evaluate(resolved, configuration);
    }

    private static DimensionEvaluation EvaluateManualProject()
    {
        var resolved = CreateResolvedDay();
        var configuration = new DimensionConfiguration(
        [
            new DimensionConfigurationLayer(
                "organisation",
                "2026.1",
                10,
                [
                    new DimensionDefinition(
                        "project",
                        "Project",
                        DimensionAssignmentMode.Manual,
                        DimensionCardinality.Exclusive,
                        false,
                        [new DimensionValue("acme", "ACME")]),
                ],
                [],
                RuleSource.Manual),
        ]);
        var assignment = DimensionAssignment.Create(
            resolved.Key,
            "project",
            "acme",
            [resolved.WorkedIntervals[0]],
            ActorRef.Employee("ada"),
            DateTimeOffset.UtcNow);
        return new DimensionEvaluator().Evaluate(
            resolved,
            configuration,
            [assignment]);
    }

    private static ResolvedWorkDay CreateResolvedDay()
    {
        var date = new DateOnly(2026, 10, 1);
        var registration = WorkRegistration.Manual(
            new WorkDayKey("ada", date),
            [
                new WorkInterval(
                    new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 10, 1, 16, 0, 0, TimeSpan.Zero)),
            ],
            ActorRef.Employee("ada"),
            ConfigurationSnapshotId.New(),
            DateTimeOffset.UtcNow);
        var shape = new DayShape(
            date,
            true,
            TimeSpan.FromHours(8),
            TimeSpan.Zero,
            null,
            [],
            [new LocalTimeRange(new TimeOnly(8, 0), new TimeOnly(16, 0))],
            [],
            [],
            "UTC");
        return new WorkDayResolver().Resolve(registration, shape);
    }
}
