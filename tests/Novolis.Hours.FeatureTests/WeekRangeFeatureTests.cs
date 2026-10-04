using Novolis.Hours.Client;

namespace Novolis.Hours.FeatureTests;

public sealed class WeekRangeFeatureTests
{
    [Test]
    public async Task Week_range_returns_structured_clocks_and_brushes_without_changing_ada_may_day()
    {
        await using var fixture = await HoursAcceptanceFixture.StartAsync();
        var ada = await fixture.ConnectAsync("ada");
        var helen = await fixture.ConnectAsync("helen");

        var week = await ada.GetWorkDaysAsync("ada", new DateOnly(2026, 9, 28), new DateOnly(2026, 10, 4));
        await Assert.That(week).Count().IsEqualTo(7);
        var thursday = week.Single(day => day.NominalDate == new DateOnly(2026, 10, 1));
        await Assert.That(thursday.WorkEnvelopeRange).IsEqualTo(
            new Novolis.Hours.Contracts.LocalTimeRangeDto(new TimeOnly(7, 0), new TimeOnly(17, 0)));
        await Assert.That(thursday.BrushCatalog.Any(brush => brush.Id == "customer")).IsTrue();
        await Assert.That(thursday.OrganisationId).IsEqualTo("nordvik-office");

        var adaMay = await ada.GetWorkDayAsync("ada", new DateOnly(2026, 5, 1));
        await helen.PublishConfigurationAsync(
            "pierre",
            new Novolis.Hours.Contracts.PublishConfigurationRequest(new DateOnly(2026, 5, 1)));
        var adaMayAfter = await ada.GetWorkDayAsync("ada", new DateOnly(2026, 5, 1));
        await Assert.That(adaMayAfter.AppliedRules.Select(rule => rule.RuleId))
            .IsEquivalentTo(adaMay.AppliedRules.Select(rule => rule.RuleId));
    }
}
