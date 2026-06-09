namespace LiveStudio.Shared.Launcher;

public static class LiveLauncherEndpoints
{
    public const string PipeName = "Novolis.LiveStudio.Launcher";

    public static TimeSpan ClientConnectTimeout { get; } = TimeSpan.FromSeconds(8);

    public static TimeSpan HostReadyTimeout { get; } = TimeSpan.FromSeconds(45);
}
