using Novolis.Avalonia.Cad.Core;

namespace DraftStudio.Unit;

public sealed class DraftUnitsTests
{
    [Test]
    [Arguments(CadUnits.Meter, 1.0, 1.0)]
    [Arguments(CadUnits.Millimeter, 1.0, 1000.0)]
    [Arguments(CadUnits.Centimeter, 2.5, 250.0)]
    [Arguments(CadUnits.Inch, 1.0, 39.37007874015748)]
    public async Task ToDisplay_Converts_Meters(string unit, double meters, double expected)
    {
        await Assert.That(Math.Abs(CadUnits.ToDisplay(meters, unit) - expected)).IsLessThan(1e-9);
    }

    [Test]
    [Arguments(CadUnits.Millimeter, 1000.0, 1.0)]
    [Arguments(CadUnits.Centimeter, 100.0, 1.0)]
    public async Task ToMeters_RoundTrips(string unit, double display, double meters)
    {
        await Assert.That(Math.Abs(CadUnits.ToMeters(display, unit) - meters)).IsLessThan(1e-9);
    }

    [Test]
    public async Task NiceScaleBar_Picks_Round_Label()
    {
        var (meters, label) = CadUnits.NiceScaleBar(metersPerPixel: 0.01, CadUnits.Meter, targetPixels: 100);
        await Assert.That(meters).IsGreaterThan(0);
        await Assert.That(label).Contains("m");
    }

    [Test]
    [Arguments(CadUnits.Meter, "m")]
    [Arguments(CadUnits.Millimeter, "mm")]
    [Arguments(CadUnits.Inch, "in")]
    public async Task Abbreviation_Known(string unit, string abbr)
    {
        await Assert.That(CadUnits.Abbreviation(unit)).IsEqualTo(abbr);
    }

    [Test]
    public async Task FormatLength_Includes_Unit()
    {
        await Assert.That(CadUnits.FormatLength(1.5, CadUnits.Meter)).IsEqualTo("1.5 m");
    }
}
