using DraftStudio.Core;

namespace DraftStudio.Unit;

public sealed class DraftUnitsTests
{
    [Test]
    [Arguments(DraftUnits.Meter, 1.0, 1.0)]
    [Arguments(DraftUnits.Millimeter, 1.0, 1000.0)]
    [Arguments(DraftUnits.Centimeter, 2.5, 250.0)]
    [Arguments(DraftUnits.Inch, 1.0, 39.37007874015748)]
    public async Task ToDisplay_Converts_Meters(string unit, double meters, double expected)
    {
        await Assert.That(Math.Abs(DraftUnits.ToDisplay(meters, unit) - expected)).IsLessThan(1e-9);
    }

    [Test]
    [Arguments(DraftUnits.Millimeter, 1000.0, 1.0)]
    [Arguments(DraftUnits.Centimeter, 100.0, 1.0)]
    public async Task ToMeters_RoundTrips(string unit, double display, double meters)
    {
        await Assert.That(Math.Abs(DraftUnits.ToMeters(display, unit) - meters)).IsLessThan(1e-9);
    }

    [Test]
    public async Task NiceScaleBar_Picks_Round_Label()
    {
        var (meters, label) = DraftUnits.NiceScaleBar(metersPerPixel: 0.01, DraftUnits.Meter, targetPixels: 100);
        await Assert.That(meters).IsGreaterThan(0);
        await Assert.That(label).Contains("m");
    }

    [Test]
    [Arguments(DraftUnits.Meter, "m")]
    [Arguments(DraftUnits.Millimeter, "mm")]
    [Arguments(DraftUnits.Inch, "in")]
    public async Task Abbreviation_Known(string unit, string abbr)
    {
        await Assert.That(DraftUnits.Abbreviation(unit)).IsEqualTo(abbr);
    }

    [Test]
    public async Task FormatLength_Includes_Unit()
    {
        await Assert.That(DraftUnits.FormatLength(1.5, DraftUnits.Meter)).IsEqualTo("1.5 m");
    }
}
