namespace Novolis.Apps.Manifest;

internal sealed class UpdateConfig
{
    public bool Enabled { get; set; } = true;
    public string AppId { get; set; } = "";
    public string Repository { get; set; } = ReleaseTrace.Repository;
    public string Channel { get; set; } = "stable";
    public string Distribution { get; set; } = "direct-github";
}
