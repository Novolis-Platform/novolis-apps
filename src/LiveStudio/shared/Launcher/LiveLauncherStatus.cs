namespace LiveStudio.Shared.Launcher;

public sealed record LiveLauncherStatus(
    LiveLauncherState State,
    string Message,
    int RestartCount,
    DateTimeOffset Timestamp)
{
    public bool IsHostReady => State == LiveLauncherState.HostRunning;

    public bool IsFatal => State == LiveLauncherState.FatalError;

    public static LiveLauncherStatus Starting(string message) =>
        new(LiveLauncherState.Starting, message, 0, DateTimeOffset.UtcNow);

    public static LiveLauncherStatus Running(string message, int restartCount) =>
        new(LiveLauncherState.HostRunning, message, restartCount, DateTimeOffset.UtcNow);

    public static LiveLauncherStatus Restarting(string message, int restartCount) =>
        new(LiveLauncherState.Restarting, message, restartCount, DateTimeOffset.UtcNow);

    public static LiveLauncherStatus Fatal(string message, int restartCount) =>
        new(LiveLauncherState.FatalError, message, restartCount, DateTimeOffset.UtcNow);

    public static LiveLauncherStatus ShuttingDown(string message) =>
        new(LiveLauncherState.ShuttingDown, message, 0, DateTimeOffset.UtcNow);
}
