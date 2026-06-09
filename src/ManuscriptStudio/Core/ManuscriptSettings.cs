namespace ManuscriptStudio.Core;

internal sealed class ManuscriptSettings
{
    public LayoutSettings Layout { get; set; } = new();

    public string? LastWorkspaceRoot { get; set; }

    public string? ContentRoot { get; set; }

    public string ActiveExtensionId { get; set; } = "generic-markdown";

    public BookAuthoringSettings BookAuthoring { get; set; } = new();
}

internal sealed class LayoutSettings
{
    public double LeftColumnPixels { get; set; } = 280;

    public double RightColumnPixels { get; set; } = 420;
}

internal sealed class BookAuthoringSettings
{
    public string? SeriesId { get; set; }

    public string? BookId { get; set; }

    public bool DebugMetadata { get; set; }

    public string RightRailView { get; set; } = "preview";
}
