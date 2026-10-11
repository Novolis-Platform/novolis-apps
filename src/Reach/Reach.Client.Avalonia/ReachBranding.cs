using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Avalonia.GraphicalProfile;
using Ngp = Novolis.Avalonia.GraphicalProfile.GraphicalProfile;
using NgpBinding = Novolis.Avalonia.GraphicalProfile.GraphicalProfileBinding;

namespace Novolis.Avalonia.Reach;

/// <summary>Shared Reach product identity for native clients and evidence captures.</summary>
public static class ReachBranding
{
    /// <summary>Full product name.</summary>
    public const string ProductName = "Novolis Reach";

    /// <summary>Short product label used where space is limited.</summary>
    public const string ShortName = "Reach";

    /// <summary>Small descriptor shown beneath the product name.</summary>
    public const string Tagline = "Private-network control, clearly connected.";

    internal static Control BuildHeader()
    {
        var glyph = new TextBlock
        {
            Text = "R",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 24,
            FontWeight = FontWeight.Bold,
        };
        NgpBinding.Bind(
            glyph,
            TextBlock.ForegroundProperty,
            Ngp.OnAccentFillResourceKey);

        var mark = new Border
        {
            Width = 46,
            Height = 46,
            CornerRadius = new CornerRadius(14),
            Child = glyph,
        };
        NgpBinding.Bind(
            mark,
            Border.BackgroundProperty,
            Ngp.AccentFillResourceKey);
        mark.SetValue(
            AutomationProperties.AutomationIdProperty,
            "ReachBrandMark");

        var eyebrow = new TextBlock
        {
            Text = "NOVOLIS / PRIVATE NETWORK",
        };
        eyebrow.Classes.Add("eyebrow");

        var title = new TextBlock
        {
            Text = ProductName,
        };
        title.Classes.Add("page-title");

        var tagline = new TextBlock
        {
            Text = Tagline,
            TextWrapping = TextWrapping.Wrap,
        };
        tagline.Classes.Add("body-copy");

        var copy = new StackPanel
        {
            Spacing = 1,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { eyebrow, title, tagline },
        };

        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { mark, copy },
        };
        header.SetValue(
            AutomationProperties.AutomationIdProperty,
            "ReachBrandHeader");
        return header;
    }
}
