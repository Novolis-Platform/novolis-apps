using Novolis.Hours.Domain.Calendars;
using Novolis.Hours.Domain.Configuration;
using Novolis.Hours.Domain.Dimensions;
using Novolis.Hours.Domain.Work;

namespace Novolis.Hours.FeatureTests;

public sealed class DimensionEvaluationFeatureTests
{
    [Test]
    public async Task Sparse_dimension_layers_stack_and_derived_rules_keep_provenance()
    {
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
                        true,
                        [new DimensionValue("acme", "ACME")]),
                    new DimensionDefinition(
                        "billability",
                        "Billability",
                        DimensionAssignmentMode.Derived,
                        DimensionCardinality.Exclusive,
                        false,
                        [new DimensionValue("billable", "Billable")]),
                ],
                [
                    new DimensionRuleRegistration(
                        "all-work-billable",
                        10,
                        new RuleRef("all-work-billable", "1", RuleSource.Manual),
                        new WorkedIntervalDimensionRule(
                            "billability",
                            "billable",
                            DimensionMeasureSource.Derived,
                            new RuleRef("all-work-billable", "1", RuleSource.Manual))),
                ],
                RuleSource.Manual),
            new DimensionConfigurationLayer(
                "employment",
                "2026.2",
                20,
                [
                    new DimensionDefinition(
                        "cost-centre",
                        "Cost centre",
                        DimensionAssignmentMode.Manual,
                        DimensionCardinality.Additive,
                        false,
                        [new DimensionValue("platform", "Platform")]),
                ],
                [],
                RuleSource.Manual),
        ]);

        var resolved = CreateResolvedDay();
        var result = new DimensionEvaluator().Evaluate(resolved, configuration);

        await Assert.That(configuration.GetDefinition("cost-centre")).IsNotNull();
        await Assert.That(result.Measures).Contains(measure =>
            measure.DimensionId == "billability" &&
            measure.ValueId == "billable" &&
            measure.Source == DimensionMeasureSource.Derived &&
            measure.Rule?.RuleId == "all-work-billable");
        await Assert.That(result.ActualWorked).IsEqualTo(TimeSpan.FromHours(8));
    }

    [Test]
    public async Task Manual_project_assignment_can_be_corrected_without_creating_work()
    {
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
                        true,
                        [
                            new DimensionValue("acme", "ACME"),
                            new DimensionValue("internal", "Internal"),
                        ]),
                ],
                [],
                RuleSource.Manual),
        ]);
        var resolved = CreateResolvedDay();
        var actor = ActorRef.Employee("ada");
        var first = DimensionAssignment.Create(
            resolved.Key,
            "project",
            "acme",
            [new WorkInterval(
                resolved.WorkedIntervals[0].Start,
                resolved.WorkedIntervals[0].Start.AddHours(4))],
            actor,
            DateTimeOffset.UtcNow);
        var correction = DimensionAssignment.Correction(
            resolved.Key,
            first.Id,
            "project",
            "internal",
            [new WorkInterval(
                new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero))],
            actor,
            DateTimeOffset.UtcNow);

        var result = new DimensionEvaluator().Evaluate(
            resolved,
            configuration,
            [first, correction]);

        await Assert.That(result.ActualWorked).IsEqualTo(TimeSpan.FromHours(8));
        await Assert.That(result.Measures).Contains(measure =>
            measure.DimensionId == "project" &&
            measure.ValueId == "internal" &&
            measure.Duration == TimeSpan.FromHours(2));
        await Assert.That(result.Measures).DoesNotContain(measure =>
            measure.DimensionId == "project" &&
            measure.ValueId == "acme");
        await Assert.That(result.MissingCoverage).Contains("project");
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
