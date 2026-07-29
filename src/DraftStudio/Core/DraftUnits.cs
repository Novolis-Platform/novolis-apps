namespace DraftStudio.Core;

/// <summary>Display-unit helpers. Document coordinates stay SI meters (novolis.cad).</summary>
internal static class DraftUnits
{
    public const string Meter = "meter";
    public const string Centimeter = "centimeter";
    public const string Millimeter = "millimeter";
    public const string Inch = "inch";

    public static readonly string[] Choices = [Meter, Centimeter, Millimeter, Inch];

    public static string Abbreviation(string unit) => unit switch
    {
        Centimeter => "cm",
        Millimeter => "mm",
        Inch => "in",
        _ => "m",
    };

    /// <summary>How many display units equal one meter.</summary>
    public static double PerMeter(string unit) => unit switch
    {
        Centimeter => 100.0,
        Millimeter => 1000.0,
        Inch => 39.37007874015748,
        _ => 1.0,
    };

    public static double ToDisplay(double meters, string unit) => meters * PerMeter(unit);

    public static double ToMeters(double display, string unit) => display / PerMeter(unit);

    public static string FormatLength(double meters, string unit, string format = "0.##")
    {
        var v = ToDisplay(meters, unit);
        return string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{v.ToString(format, System.Globalization.CultureInfo.InvariantCulture)} {Abbreviation(unit)}");
    }

    /// <summary>Pick a round scale-bar length (in meters) that lands near <paramref name="targetPixels"/>.</summary>
    public static (double Meters, string Label) NiceScaleBar(double metersPerPixel, string unit, double targetPixels = 96)
    {
        var targetMeters = Math.Max(1e-9, metersPerPixel * targetPixels);
        var targetDisplay = ToDisplay(targetMeters, unit);
        var niceDisplay = NiceNumber(targetDisplay);
        var meters = ToMeters(niceDisplay, unit);
        var label = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{niceDisplay.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)} {Abbreviation(unit)}");
        return (meters, label);
    }

    private static double NiceNumber(double value)
    {
        if (value <= 0)
            return 1;
        var exp = Math.Floor(Math.Log10(value));
        var f = value / Math.Pow(10, exp);
        var nice = f < 1.5 ? 1 : f < 3.5 ? 2 : f < 7.5 ? 5 : 10;
        return nice * Math.Pow(10, exp);
    }
}
