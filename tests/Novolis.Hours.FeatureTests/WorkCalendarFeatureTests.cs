using Novolis.Hours.Domain.Calendars;
using Novolis.Hours.Domain.Configuration;
using Novolis.Hours.Domain.Work;

namespace Novolis.Hours.FeatureTests;

public sealed class WorkCalendarFeatureTests
{
    [Test]
    public async Task Ordered_calendar_layers_preserve_rule_provenance_and_sparse_overrides()
    {
        var calendars = new CalendarRuleSet(
        [
            new Calendar(
                "national",
                "2026.1",
                10,
                [
                    new WeekdayCalendarRule(
                        "national-weekdays",
                        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
                        [new WorkingDayRule(false), new DayTagRule("calendar", "weekday")]),
                ],
                RuleSource.BuiltIn),
            new Calendar(
                "agreement",
                "2026.2",
                20,
                [
                    new FixedDateCalendarRule(
                        "agreement-paid-christmas",
                        new DateOnly(2026, 12, 25),
                        [
                            new WorkingDayRule(true),
                            new ExpectedWorkRule(TimeSpan.FromHours(7.5)),
                            new PaidEntitlementRule(TimeSpan.FromHours(7.5)),
                            new DayTagRule("holiday", "Christmas Day"),
                        ]),
                ],
                RuleSource.Manual),
        ],
        "Europe/Oslo");

        var shape = calendars.GetDayShape(new DateOnly(2026, 12, 25));

        await Assert.That(shape.IsWorkingDay).IsTrue();
        await Assert.That(shape.ExpectedWork).IsEqualTo(TimeSpan.FromHours(7.5));
        await Assert.That(shape.PaidEntitlement).IsEqualTo(TimeSpan.FromHours(7.5));
        await Assert.That(shape.Tags).Contains(new DayTag("holiday", "Christmas Day"));
        await Assert.That(shape.Rules.Length).IsEqualTo(6);
        await Assert.That(shape.Rules[^1].CalendarId).IsEqualTo("agreement");
        await Assert.That(shape.Rules[^1].Source).IsEqualTo(RuleSource.Manual);
    }

    [Test]
    public async Task Worked_as_scheduled_resolves_routine_without_claiming_a_clock_observation()
    {
        var calendars = new CalendarRuleSet(
            [
                new Calendar(
                    "employment",
                    "2026.1",
                    10,
                    [
                        new EveryDateCalendarRule(
                            "routine",
                            [
                                new WorkingDayRule(true),
                                new ExpectedWorkRule(TimeSpan.FromHours(7.5)),
                                new RoutineWorkRule(
                                [
                                    new LocalTimeRange(new TimeOnly(8, 0), new TimeOnly(11, 30)),
                                    new LocalTimeRange(new TimeOnly(12, 0), new TimeOnly(16, 0)),
                                ]),
                            ]),
                    ],
                    RuleSource.Manual),
            ],
            "UTC");
        var registration = WorkRegistration.WorkedAsScheduled(
            new WorkDayKey("ada", new DateOnly(2026, 10, 1)),
            ActorRef.Employee("ada"),
            ConfigurationSnapshotId.New(),
            DateTimeOffset.UtcNow);

        var resolved = new WorkDayResolver().Resolve(
            registration,
            calendars.GetDayShape(registration.WorkDay.NominalDate));

        await Assert.That(resolved.WorkedIntervals.Length).IsEqualTo(2);
        await Assert.That(resolved.ActualWorked).IsEqualTo(TimeSpan.FromHours(7.5));
        await Assert.That(resolved.WorkedIntervals[0].Start).IsEqualTo(
            new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
        await Assert.That(resolved.EffectiveRegistration.Intervals).IsEmpty();
        await Assert.That(resolved.RoutineComparison.OutsideRoutine).IsEqualTo(TimeSpan.Zero);
    }

    [Test]
    public async Task A_split_manual_workday_can_cross_midnight_and_is_only_a_routine_difference()
    {
        var date = new DateOnly(2026, 10, 1);
        var registration = WorkRegistration.Manual(
            new WorkDayKey("ada", date),
            [
                new WorkInterval(
                    new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 10, 1, 15, 0, 0, TimeSpan.Zero)),
                new WorkInterval(
                    new DateTimeOffset(2026, 10, 1, 20, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 10, 2, 2, 0, 0, TimeSpan.Zero)),
            ],
            ActorRef.Employee("ada"),
            ConfigurationSnapshotId.New(),
            DateTimeOffset.UtcNow);
        var shape = new DayShape(
            date,
            true,
            TimeSpan.FromHours(7.5),
            TimeSpan.Zero,
            null,
            [],
            [
                new LocalTimeRange(new TimeOnly(8, 0), new TimeOnly(16, 0)),
            ],
            [],
            [],
            "UTC");

        var resolved = new WorkDayResolver().Resolve(registration, shape);

        await Assert.That(resolved.WorkedIntervals.Length).IsEqualTo(2);
        await Assert.That(resolved.ActualWorked).IsEqualTo(TimeSpan.FromHours(13));
        await Assert.That(resolved.RoutineComparison.OutsideRoutine).IsEqualTo(TimeSpan.FromHours(6));
        await Assert.That(resolved.RoutineComparison.Differences)
            .Contains(difference => difference.Kind == RoutineDifferenceKind.SplitWorkDay);
    }
}
