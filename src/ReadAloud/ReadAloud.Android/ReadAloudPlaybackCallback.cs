using Android.Media.Session;

namespace ReadAloud.Android;

/// <summary>Receives headset, lock-screen, and Bluetooth transport commands.</summary>
public sealed class ReadAloudPlaybackCallback : MediaSession.Callback
{
    public override void OnPlay() => AndroidMp3Player.Active?.Resume();

    public override void OnPause() => AndroidMp3Player.Active?.Pause();

    public override void OnStop() => AndroidMp3Player.RequestExternalStop();
}
