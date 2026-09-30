using Avalonia.Media;
using Novolis.Avalonia.GraphicalProfile;

namespace PresenceLedger.App;

/// <summary>Semantic visual roles for the Presence Ledger shell.</summary>
internal static class PresencePalette
{
    public static Color Background => GraphicalProfile.Background;
    public static Color Surface => GraphicalProfile.Surface;
    public static Color SurfaceRaised => GraphicalProfile.Raised;
    public static Color Border => GraphicalProfile.Border;
    public static Color Text => GraphicalProfile.Text;
    public static Color Muted => GraphicalProfile.Muted;
    public static Color Teal => GraphicalProfile.Accent;
    public static Color TealDeep => GraphicalProfile.AccentFill;
    public static Color Copper => GraphicalProfile.Action;
    public static Color CopperSoft => GraphicalProfile.ActionSoft;
    public static Color Warning => GraphicalProfile.Warning;
    public static Color Danger => GraphicalProfile.Danger;

    public static IBrush BackgroundBrush => GraphicalProfile.BackgroundBrush;
    public static IBrush SurfaceBrush => GraphicalProfile.SurfaceBrush;
    public static IBrush RaisedBrush => GraphicalProfile.RaisedBrush;
    public static IBrush BorderBrush => GraphicalProfile.BorderBrush;
    public static IBrush TextBrush => GraphicalProfile.TextBrush;
    public static IBrush MutedBrush => GraphicalProfile.MutedBrush;
    public static IBrush TealBrush => GraphicalProfile.AccentBrush;
    public static IBrush TealDeepBrush => GraphicalProfile.AccentFillBrush;
    public static IBrush CopperBrush => GraphicalProfile.ActionBrush;
    public static IBrush CopperSoftBrush => GraphicalProfile.ActionSoftBrush;
    public static IBrush WarningBrush => GraphicalProfile.WarningBrush;
    public static IBrush DangerBrush => GraphicalProfile.DangerBrush;
}
