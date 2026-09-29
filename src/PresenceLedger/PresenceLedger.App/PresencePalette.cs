using Avalonia.Media;

namespace PresenceLedger.App;

/// <summary>Semantic visual roles for the Presence Ledger shell.</summary>
internal static class PresencePalette
{
    public static readonly Color Background = Color.Parse("#081820");
    public static readonly Color Surface = Color.Parse("#102b35");
    public static readonly Color SurfaceRaised = Color.Parse("#163b47");
    public static readonly Color Border = Color.Parse("#2b5965");
    public static readonly Color Text = Color.Parse("#eef7f4");
    public static readonly Color Muted = Color.Parse("#9ab6ba");
    public static readonly Color Teal = Color.Parse("#43d0c2");
    public static readonly Color TealDeep = Color.Parse("#126f73");
    public static readonly Color Copper = Color.Parse("#d58b45");
    public static readonly Color CopperSoft = Color.Parse("#5f3c29");
    public static readonly Color Warning = Color.Parse("#f1c56a");
    public static readonly Color Danger = Color.Parse("#ef8d82");

    public static IBrush BackgroundBrush => Brush(Background);
    public static IBrush SurfaceBrush => Brush(Surface);
    public static IBrush RaisedBrush => Brush(SurfaceRaised);
    public static IBrush BorderBrush => Brush(Border);
    public static IBrush TextBrush => Brush(Text);
    public static IBrush MutedBrush => Brush(Muted);
    public static IBrush TealBrush => Brush(Teal);
    public static IBrush TealDeepBrush => Brush(TealDeep);
    public static IBrush CopperBrush => Brush(Copper);
    public static IBrush CopperSoftBrush => Brush(CopperSoft);
    public static IBrush WarningBrush => Brush(Warning);
    public static IBrush DangerBrush => Brush(Danger);

    static IBrush Brush(Color color) => new SolidColorBrush(color);
}
