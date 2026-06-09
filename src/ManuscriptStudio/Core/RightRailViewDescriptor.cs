namespace ManuscriptStudio.Core;

internal sealed record RightRailViewDescriptor(string Id, string DisplayName)
{
    public static readonly RightRailViewDescriptor Preview = new("preview", "Preview");
}
