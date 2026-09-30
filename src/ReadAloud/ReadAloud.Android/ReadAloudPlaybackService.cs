using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace ReadAloud.Android;

/// <summary>Keeps Read Aloud in the foreground while audio is published to the system.</summary>
[Service(
    Name = "com.novolis.readaloud.ReadAloudPlaybackService",
    Exported = false,
    ForegroundServiceType = ForegroundService.TypeMediaPlayback)]
public sealed class ReadAloudPlaybackService : Service
{
    public const string ActionShow = "com.novolis.readaloud.playback.SHOW";
    public const string ActionPause = "com.novolis.readaloud.playback.PAUSE";
    public const string ActionResume = "com.novolis.readaloud.playback.RESUME";
    public const string ActionStop = "com.novolis.readaloud.playback.STOP";

    public override void OnCreate()
    {
        base.OnCreate();
        ReadAloudPlaybackSession.Current?.AttachService(this);
    }

    public override StartCommandResult OnStartCommand(
        Intent? intent,
        StartCommandFlags flags,
        int startId)
    {
        ReadAloudPlaybackSession.Current?.AttachService(this);
        switch (intent?.Action)
        {
            case ActionPause:
                AndroidMp3Player.Active?.Pause();
                break;
            case ActionResume:
                AndroidMp3Player.Active?.Resume();
                break;
            case ActionStop:
                AndroidMp3Player.RequestExternalStop();
                break;
        }

        return StartCommandResult.NotSticky;
    }

    public override IBinder? OnBind(Intent? intent) => null;
}
