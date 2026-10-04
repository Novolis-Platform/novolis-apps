using Novolis.Hours.Application;
using Novolis.Hours.Domain.Calendars;

namespace Novolis.Hours.FeatureTests;

public sealed class CalendarStackingFeatureTests
{
    [Test]
    public async Task Nordvik_office_weekday_keeps_expected_work_routine_and_layer_order()
    {
        var shape = Shape("ada", new DateOnly(2026, 10, 1));

        await Assert.That(shape.IsWorkingDay).IsTrue();
        await Assert.That(shape.ExpectedWork).IsEqualTo(TimeSpan.FromHours(7.5));
        await Assert.That(shape.TimeZoneId).IsEqualTo("Europe/Oslo");
        await Assert.That(shape.WorkEnvelope).IsEqualTo(new LocalTimeRange(new TimeOnly(7, 0), new TimeOnly(17, 0)));
        await Assert.That(shape.CoreHours).Contains(new LocalTimeRange(new TimeOnly(9, 0), new TimeOnly(15, 0)));
        await Assert.That(shape.RoutineWork).Contains(new LocalTimeRange(new TimeOnly(8, 0), new TimeOnly(11, 30)));
        await Assert.That(shape.Rules.Select(rule => rule.LayerKind))
            .Contains(CalendarLayerKind.National);
        await Assert.That(shape.Rules.Select(rule => rule.LayerKind))
            .Contains(CalendarLayerKind.Employee);
        await Assert.That(shape.Rules[^1].LayerKind).IsEqualTo(CalendarLayerKind.Employee);
    }

    [Test]
    public async Task Nordvik_office_weekend_is_non_working_without_erasing_later_silence()
    {
        var shape = Shape("ada", new DateOnly(2026, 10, 4));

        await Assert.That(shape.IsWorkingDay).IsFalse();
        await Assert.That(shape.ExpectedWork).IsEqualTo(TimeSpan.Zero);
        await Assert.That(shape.Rules).Contains(rule =>
            rule.LayerKind == CalendarLayerKind.National &&
            rule.Rule is WorkingDayRule { Value: false });
    }

    [Test]
    public async Task Nordvik_office_may_day_is_norwegian_public_holiday_not_french_labour_day()
    {
        var norway = Shape("ada", new DateOnly(2026, 5, 1));
        var france = Shape("pierre", new DateOnly(2026, 5, 1));

        await Assert.That(norway.IsWorkingDay).IsFalse();
        await Assert.That(norway.Tags).Contains(tag =>
            tag.Key == "PublicHoliday" && tag.Value.Contains("mai", StringComparison.OrdinalIgnoreCase));
        await Assert.That(norway.Tags).DoesNotContain(tag =>
            tag.Value.Contains("Travail", StringComparison.OrdinalIgnoreCase));
        await Assert.That(france.IsWorkingDay).IsFalse();
        await Assert.That(france.Tags).Contains(tag =>
            tag.Key == "PublicHoliday" && tag.Value.Contains("Travail", StringComparison.OrdinalIgnoreCase));
        await Assert.That(norway.Rules).Contains(rule =>
            rule.LayerKind == CalendarLayerKind.National &&
            rule.Provenance is { Jurisdiction: "NO", SourcePackage: "PublicHoliday" });
        await Assert.That(france.Rules).Contains(rule =>
            rule.LayerKind == CalendarLayerKind.National &&
            rule.Provenance is { Jurisdiction: "FR" });
    }

    [Test]
    public async Task Nordvik_office_christmas_eve_is_a_corporate_paid_day()
    {
        var shape = Shape("ada", new DateOnly(2026, 12, 24));

        await Assert.That(shape.IsWorkingDay).IsFalse();
        await Assert.That(shape.ExpectedWork).IsEqualTo(TimeSpan.Zero);
        await Assert.That(shape.PaidEntitlement).IsEqualTo(TimeSpan.FromHours(7.5));
        await Assert.That(shape.Tags).Contains(new DayTag("PaidEntitlement", "Corporate Christmas Eve"));
        await Assert.That(shape.Rules).Contains(rule =>
            rule.RuleId == "corporate-christmas-eve" &&
            rule.LayerKind == CalendarLayerKind.Organisation);
    }

    [Test]
    public async Task Nordvik_station_sunday_override_keeps_the_national_weekend_rule_in_the_explanation()
    {
        var shape = Shape("bob", new DateOnly(2026, 10, 4));

        await Assert.That(shape.IsWorkingDay).IsTrue();
        await Assert.That(shape.ExpectedWork).IsEqualTo(TimeSpan.FromHours(7.5));
        await Assert.That(shape.Rules).Contains(rule =>
            rule.LayerKind == CalendarLayerKind.National &&
            rule.Rule is WorkingDayRule { Value: false });
        await Assert.That(shape.Rules).Contains(rule =>
            rule.LayerKind == CalendarLayerKind.Organisation &&
            rule.Rule is WorkingDayRule { Value: true });
    }

    [Test]
    public async Task Nordvik_station_christmas_is_a_working_public_holiday_with_paid_entitlement()
    {
        var shape = Shape("bob", new DateOnly(2026, 12, 25));

        await Assert.That(shape.IsWorkingDay).IsTrue();
        await Assert.That(shape.ExpectedWork).IsEqualTo(TimeSpan.FromHours(7.5));
        await Assert.That(shape.PaidEntitlement).IsEqualTo(TimeSpan.FromHours(7.5));
        await Assert.That(shape.Tags).Contains(tag => tag.Key == "PublicHoliday");
        await Assert.That(shape.Rules).Contains(rule =>
            rule.LayerKind == CalendarLayerKind.National &&
            rule.Provenance is { HolidayId: "førstejuledag" });
        await Assert.That(shape.Rules).Contains(rule =>
            rule.RuleId == "christmas-entitlement" &&
            rule.LayerKind == CalendarLayerKind.Agreement);
    }

    [Test]
    public async Task Ada_temporary_override_changes_only_the_envelope()
    {
        var ordinary = Shape("ada", new DateOnly(2026, 11, 17));
        var overridden = Shape("ada", HoursCustomerCatalog.AdaTemporaryOverrideDate);

        await Assert.That(ordinary.WorkEnvelope).IsEqualTo(new LocalTimeRange(new TimeOnly(7, 0), new TimeOnly(17, 0)));
        await Assert.That(overridden.IsWorkingDay).IsTrue();
        await Assert.That(overridden.ExpectedWork).IsEqualTo(TimeSpan.FromHours(7.5));
        await Assert.That(overridden.WorkEnvelope).IsEqualTo(new LocalTimeRange(new TimeOnly(8, 0), new TimeOnly(18, 0)));
        await Assert.That(overridden.Rules).Contains(rule =>
            rule.RuleId == "ada-temporary-envelope" &&
            rule.LayerKind == CalendarLayerKind.TemporaryOverride);
    }

    [Test]
    public async Task Atelier_curie_mid_year_agreement_changes_envelope_without_rewriting_earlier_days()
    {
        var before = Shape("pierre", new DateOnly(2026, 5, 15).AddDays(-1));
        var after = Shape("pierre", new DateOnly(2026, 5, 15));

        await Assert.That(HoursCustomerCatalog.GetCalendarVersion("pierre", new DateOnly(2026, 5, 14)))
            .IsEqualTo("agreement-v1");
        await Assert.That(HoursCustomerCatalog.GetCalendarVersion("pierre", new DateOnly(2026, 5, 15)))
            .IsEqualTo("agreement-v2");
        await Assert.That(after.WorkEnvelope).IsEqualTo(new LocalTimeRange(new TimeOnly(8, 30), new TimeOnly(16, 30)));
        await Assert.That(after.TimeZoneId).IsEqualTo("Europe/Paris");
        await Assert.That(before.Rules.Select(rule => rule.CalendarVersion).Distinct())
            .Contains("agreement-v1");
        await Assert.That(after.Rules.Select(rule => rule.CalendarVersion).Distinct())
            .Contains("agreement-v2");
    }

    [Test]
    public async Task Warsaw_and_helsinki_keep_their_national_holidays_and_time_zones()
    {
        var poland = Shape("anna", new DateOnly(2026, 5, 3));
        var finland = Shape("liisa", new DateOnly(2026, 12, 6));

        await Assert.That(poland.IsWorkingDay).IsFalse();
        await Assert.That(poland.TimeZoneId).IsEqualTo("Europe/Warsaw");
        await Assert.That(poland.Tags).Contains(tag =>
            tag.Key == "PublicHoliday" && tag.Value.Contains("Constitution", StringComparison.OrdinalIgnoreCase));
        await Assert.That(finland.IsWorkingDay).IsFalse();
        await Assert.That(finland.TimeZoneId).IsEqualTo("Europe/Helsinki");
        await Assert.That(finland.Tags).Contains(tag =>
            tag.Key == "PublicHoliday" && tag.Value.Contains("Itsen", StringComparison.OrdinalIgnoreCase));
        await Assert.That(HoursCustomerCatalog.OrganisationId("anna")).IsEqualTo(HoursCustomerCatalog.WarsawSettlement);
        await Assert.That(HoursCustomerCatalog.OrganisationId("liisa")).IsEqualTo(HoursCustomerCatalog.HelsinkiFlex);
    }

    [Test]
    public async Task Game_retail_keeps_saturday_shop_hours_and_closes_sunday()
    {
        var saturday = Shape("jamie", new DateOnly(2026, 3, 14));
        var sunday = Shape("jamie", new DateOnly(2026, 3, 15));
        var christmas = Shape("jamie", new DateOnly(2026, 12, 25));
        var boxingObserved = Shape("jamie", new DateOnly(2026, 12, 28));

        await Assert.That(saturday.IsWorkingDay).IsTrue();
        await Assert.That(saturday.ExpectedWork).IsEqualTo(TimeSpan.FromHours(8));
        await Assert.That(saturday.TimeZoneId).IsEqualTo("Europe/London");
        await Assert.That(saturday.WorkEnvelope).IsEqualTo(new LocalTimeRange(new TimeOnly(9, 0), new TimeOnly(18, 0)));
        await Assert.That(sunday.IsWorkingDay).IsFalse();
        await Assert.That(christmas.IsWorkingDay).IsFalse();
        await Assert.That(christmas.Tags).Contains(tag => tag.Key == "PublicHoliday");
        await Assert.That(boxingObserved.IsWorkingDay).IsFalse();
        await Assert.That(boxingObserved.Tags).Contains(tag =>
            tag.Key == "PublicHoliday" && tag.Value.Contains("Boxing", StringComparison.OrdinalIgnoreCase));
        await Assert.That(HoursCustomerCatalog.OrganisationId("jamie")).IsEqualTo(HoursCustomerCatalog.GameRetail);
        await Assert.That(HoursCustomerCatalog.ForEmployee("jamie").AttendanceConfirmationOnly).IsTrue();
    }

    [Test]
    public async Task Sparse_later_working_day_does_not_erase_an_earlier_public_holiday_tag()
    {
        var shape = Shape("bob", new DateOnly(2026, 12, 25));
        var holiday = shape.Rules.Single(rule => rule.Provenance?.HolidayId == "førstejuledag");

        await Assert.That(holiday.Rule).IsTypeOf<DayTagRule>();
        await Assert.That(((DayTagRule)holiday.Rule).Key).IsEqualTo("PublicHoliday");
        await Assert.That(shape.Rules).Contains(rule =>
            rule.LayerKind == CalendarLayerKind.Organisation &&
            rule.Rule is WorkingDayRule { Value: true });
    }

    private static DayShape Shape(string employeeId, DateOnly date) =>
        HoursCustomerCatalog.GetCalendar(employeeId, date).GetDayShape(date);
}
