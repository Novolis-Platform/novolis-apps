using System.Runtime.Versioning;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics.Drawables;
using Android.Media;
using Android.Media.Session;
using Android.OS;

namespace ReadAloud.Android;

/// <summary>
/// Publishes Read Aloud as an active media session so the shade, lock screen,
/// and headset can pause, resume, and stop the current listen.
/// </summary>
public sealed class ReadAloudPlaybackSession : Java.Lang.Object, AudioManager.IOnAudioFocusChangeListener
{
    public const int NotificationId = 41021;
    public const int NotificationPermissionRequest = 41021;
    const string ChannelId = "readaloud.playback";

    public static ReadAloudPlaybackSession? Current { get; private set; }

    readonly Handler _main = new(Looper.MainLooper!);
    MediaSession? _session;
    AudioAttributes? _attributes;
    AudioFocusRequestClass? _focusRequest;
    Service? _service;
    PlaybackStateCode _state = PlaybackStateCode.None;
    long _position;
    long _duration;
    long _publishedDuration = -1;
    float _speed;
    bool _foreground;
    bool _serviceStartPosted;
    bool _focusHeld;
    bool _resumeAfterTransientLoss;

    public ReadAloudPlaybackSession()
    {
        Current = this;
    }

    public AudioAttributes? Attributes => _attributes;

    public void Publish(PlaybackStateCode state, long positionMs, long durationMs, float speed)
    {
        _state = state;
        _position = positionMs;
        _duration = durationMs;
        _speed = speed;
        EnsureSession();
        ApplyPlaybackState();
        ApplyMetadata();
        if (_session is not null)
            _session.Active = true;
        RequestFocus();

        if (_foreground)
            UpdateNotification();
        else if (!_serviceStartPosted)
            StartService();
    }

    public void UpdatePosition(long positionMs, long durationMs)
    {
        if (_session is not { Active: true })
            return;

        _position = positionMs;
        if (durationMs > 0)
            _duration = durationMs;
        ApplyPlaybackState();
        ApplyMetadata();
    }

    public void End()
    {
        if (_session is null && !_serviceStartPosted && _service is null)
            return;

        _resumeAfterTransientLoss = false;
        _state = PlaybackStateCode.Stopped;
        _speed = 0;
        ApplyPlaybackState();
        if (_session is not null)
            _session.Active = false;
        AbandonFocus();

        if (_service is not null)
            ReleaseService();
    }

    public void RefreshNotification()
    {
        if (_foreground)
            UpdateNotification();
    }

    internal void AttachService(Service service)
    {
        _service = service;
        Promote(service);
        if (_state is PlaybackStateCode.None or PlaybackStateCode.Stopped)
            ReleaseService();
    }

    public void OnAudioFocusChange(AudioFocus focusChange)
    {
        _main.Post(() =>
        {
            switch (focusChange)
            {
                case AudioFocus.Loss:
                    _resumeAfterTransientLoss = false;
                    AndroidMp3Player.Active?.Pause();
                    break;
                case AudioFocus.LossTransient:
                    _resumeAfterTransientLoss = _state == PlaybackStateCode.Playing;
                    AndroidMp3Player.Active?.Pause();
                    break;
                case AudioFocus.LossTransientCanDuck:
                    AndroidMp3Player.Active?.Duck();
                    break;
                case AudioFocus.Gain:
                    AndroidMp3Player.Active?.Unduck();
                    if (_resumeAfterTransientLoss)
                    {
                        _resumeAfterTransientLoss = false;
                        AndroidMp3Player.Active?.Resume();
                    }

                    break;
            }
        });
    }

    void EnsureSession()
    {
        if (_session is not null)
            return;

        var context = global::Android.App.Application.Context
            ?? throw new InvalidOperationException("Android application context is unavailable.");
        _attributes = new AudioAttributes.Builder()
            .SetUsage(AudioUsageKind.Media)!
            .SetContentType(AudioContentType.Speech)!
            .Build();
        _session = new MediaSession(context, "ReadAloud");
        _session.SetCallback(new ReadAloudPlaybackCallback(), _main);
        if (_attributes is not null)
            _session.SetPlaybackToLocal(_attributes);
    }

    void StartService()
    {
        var context = global::Android.App.Application.Context;
        if (context is null)
            return;

        RequestNotificationPermission();
        _serviceStartPosted = true;
        var intent = new Intent(context, typeof(ReadAloudPlaybackService));
        intent.SetAction(ReadAloudPlaybackService.ActionShow);
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            context.StartForegroundService(intent);
        else
            context.StartService(intent);
    }

    void Promote(Service service)
    {
        var notification = BuildNotification();
        if (OperatingSystem.IsAndroidVersionAtLeast(29))
            service.StartForeground(NotificationId, notification, ForegroundService.TypeMediaPlayback);
        else
            service.StartForeground(NotificationId, notification);
        _foreground = true;
    }

    void ReleaseService()
    {
        if (_service is null)
            return;

        if (OperatingSystem.IsAndroidVersionAtLeast(24))
            _service.StopForeground(StopForegroundFlags.Remove);
        else
            _service.StopForeground(true);

        _service.StopSelf();
        _service = null;
        _foreground = false;
        _serviceStartPosted = false;
    }

    void ApplyPlaybackState()
    {
        if (_session is null)
            return;

        var state = new PlaybackState.Builder()
            .SetActions(
                PlaybackState.ActionPlay |
                PlaybackState.ActionPause |
                PlaybackState.ActionPlayPause |
                PlaybackState.ActionStop)!
            .SetState(_state, _position, _speed, SystemClock.ElapsedRealtime())!
            .Build();
        _session.SetPlaybackState(state);
    }

    void ApplyMetadata()
    {
        if (_session is null || _duration <= 0 || _duration == _publishedDuration)
            return;

        _publishedDuration = _duration;
        var metadata = new MediaMetadata.Builder()
            .PutString(MediaMetadata.MetadataKeyTitle, "Read Aloud")!
            .PutString(MediaMetadata.MetadataKeyArtist, "Read Aloud")!
            .PutLong(MediaMetadata.MetadataKeyDuration, _duration)!
            .Build();
        _session.SetMetadata(metadata);
    }

    void UpdateNotification()
    {
        if (_service?.GetSystemService(Context.NotificationService) is not NotificationManager manager)
            return;

        manager.Notify(NotificationId, BuildNotification());
    }

    Notification BuildNotification()
    {
        var context = (Context?)_service ?? global::Android.App.Application.Context
            ?? throw new InvalidOperationException("Android application context is unavailable.");
        var builder = OperatingSystem.IsAndroidVersionAtLeast(26)
            ? ChannelBuilder(context)
            : new Notification.Builder(context);
        return Decorate(builder, context).Build();
    }

    [SupportedOSPlatform("android26.0")]
    Notification.Builder ChannelBuilder(Context context)
    {
        if (context.GetSystemService(Context.NotificationService) is NotificationManager manager)
        {
            manager.CreateNotificationChannel(new NotificationChannel(
                ChannelId,
                "Read Aloud playback",
                NotificationImportance.Low)
            {
                Description = "Playback controls for Read Aloud.",
                LockscreenVisibility = NotificationVisibility.Public,
            });
        }

        return new Notification.Builder(context, ChannelId);
    }

    Notification.Builder Decorate(Notification.Builder builder, Context context)
    {
        var style = new Notification.MediaStyle();
        if (_session?.SessionToken is { } token)
            style.SetMediaSession(token);
        style.SetShowActionsInCompactView([0, 1]);

        builder
            .SetSmallIcon(global::Android.Resource.Drawable.IcMediaPlay)!
            .SetContentTitle("Read Aloud")!
            .SetContentText(Caption())!
            .SetOngoing(true)!
            .SetOnlyAlertOnce(true)!
            .SetVisibility(NotificationVisibility.Public)!
            .SetCategory(Notification.CategoryTransport)!
            .SetContentIntent(ContentIntent(context))!
            .SetStyle(style);

        if (_state == PlaybackStateCode.Paused)
        {
            AddTransport(
                context,
                builder,
                global::Android.Resource.Drawable.IcMediaPlay,
                "Play",
                TransportIntent(context, ReadAloudPlaybackService.ActionResume, 2));
        }
        else
        {
            AddTransport(
                context,
                builder,
                global::Android.Resource.Drawable.IcMediaPause,
                "Pause",
                TransportIntent(context, ReadAloudPlaybackService.ActionPause, 1));
        }

        AddTransport(
            context,
            builder,
            global::Android.Resource.Drawable.IcMenuCloseClearCancel,
            "Stop",
            TransportIntent(context, ReadAloudPlaybackService.ActionStop, 3));
        return builder;
    }

    static void AddTransport(
        Context context,
        Notification.Builder builder,
        int icon,
        string title,
        PendingIntent? intent)
    {
        var graphic = Icon.CreateWithResource(context, icon);
        if (graphic is null)
            return;

        var action = new Notification.Action.Builder(graphic, title, intent).Build();
        if (action is not null)
            builder.AddAction(action);
    }

    string Caption() => _state switch
    {
        PlaybackStateCode.Playing => "Playing",
        PlaybackStateCode.Paused => "Paused",
        PlaybackStateCode.Buffering => "Getting the next part",
        _ => "Stopped",
    };

    static PendingIntent? ContentIntent(Context context)
    {
        var intent = new Intent(context, typeof(MainActivity));
        intent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        return PendingIntent.GetActivity(context, 4, intent, PendingFlags());
    }

    static PendingIntent? TransportIntent(Context context, string action, int requestCode)
    {
        var intent = new Intent(context, typeof(ReadAloudPlaybackService));
        intent.SetAction(action);
        return PendingIntent.GetService(context, requestCode, intent, PendingFlags());
    }

    static PendingIntentFlags PendingFlags() =>
        PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable;

    void RequestFocus()
    {
        if (_focusHeld || _attributes is null)
            return;

        if (global::Android.App.Application.Context?.GetSystemService(Context.AudioService) is not AudioManager manager)
            return;

        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            _focusRequest ??= new AudioFocusRequestClass.Builder(AudioFocus.Gain)
                .SetAudioAttributes(_attributes)!
                .SetOnAudioFocusChangeListener(this, _main)!
                .Build();
            if (_focusRequest is not null)
                manager.RequestAudioFocus(_focusRequest);
        }
        else
        {
            manager.RequestAudioFocus(this, global::Android.Media.Stream.Music, AudioFocus.Gain);
        }

        _focusHeld = true;
    }

    void AbandonFocus()
    {
        if (!_focusHeld)
            return;

        if (global::Android.App.Application.Context?.GetSystemService(Context.AudioService) is AudioManager manager)
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                if (_focusRequest is not null)
                    manager.AbandonAudioFocusRequest(_focusRequest);
            }
            else
            {
                manager.AbandonAudioFocus(this);
            }
        }

        _focusHeld = false;
    }

    static void RequestNotificationPermission()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(33))
            return;

        var activity = MainActivity.Current;
        if (activity is null)
            return;

        if (activity.CheckSelfPermission(global::Android.Manifest.Permission.PostNotifications) == Permission.Granted)
            return;

        activity.RequestPermissions(
            [global::Android.Manifest.Permission.PostNotifications],
            NotificationPermissionRequest);
    }
}
