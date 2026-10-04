using Novolis.Hours.Application;
using Novolis.Hours.Domain.Calendars;

namespace Novolis.Hours.FeatureTests;

public sealed class WorkingTimeNormFeatureTests
{
    [Test]
    public async Task Country_stories_name_overtime_the_way_those_shops_talk()
    {
        await Assert.That(HoursWorkingTimeNorms.ForCountry("US").WeeklyOvertimeAfterMinutes).IsEqualTo(40 * 60);
        await Assert.That(HoursWorkingTimeNorms.ForCountry("US").DailyOvertimeAfterMinutes).IsEqualTo(8 * 60);
        await Assert.That(HoursWorkingTimeNorms.ForCountry("CA").WeeklyOvertimeAfterMinutes).IsEqualTo(44 * 60);
        await Assert.That(HoursWorkingTimeNorms.ForCountry("JP").OvertimeRule).Contains("Article 36");
        await Assert.That(HoursWorkingTimeNorms.ForCountry("DE").RestRule).Contains("11-hour");
        await Assert.That(HoursLocationCatalog.All.Select(item => item.Id)).Contains("united-states");
        await Assert.That(HoursLocationCatalog.All.Select(item => item.Id)).Contains("japan");
    }

    [Test]
    public async Task Seeded_yards_close_on_their_national_holidays()
    {
        var independence = HoursCustomerCatalog.GetCalendar("jordan", new DateOnly(2026, 7, 3))
            .GetDayShape(new DateOnly(2026, 7, 3));
        var canadaDay = HoursCustomerCatalog.GetCalendar("casey", new DateOnly(2026, 7, 1))
            .GetDayShape(new DateOnly(2026, 7, 1));
        var comingOfAge = HoursCustomerCatalog.GetCalendar("yuki", new DateOnly(2026, 1, 12))
            .GetDayShape(new DateOnly(2026, 1, 12));
        var unity = HoursCustomerCatalog.GetCalendar("lena", new DateOnly(2026, 10, 3))
            .GetDayShape(new DateOnly(2026, 10, 3));

        await Assert.That(independence.IsWorkingDay).IsFalse();
        await Assert.That(canadaDay.IsWorkingDay).IsFalse();
        await Assert.That(comingOfAge.IsWorkingDay).IsFalse();
        await Assert.That(unity.IsWorkingDay).IsFalse();
    }
}
