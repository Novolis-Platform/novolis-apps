using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidUri = Android.Net.Uri;
using MediaPlayer = Android.Media.MediaPlayer;
using WidgetOrientation = Android.Widget.Orientation;

namespace NovolisQuietVideo.Android;

[Activity(
    Label = "Novolis Quiet Video",
    Theme = "@style/QuietVideoTheme",
    MainLauncher = true,
    Exported = true,
    LaunchMode = LaunchMode.SingleTop,
    ExcludeFromRecents = true,
    ConfigurationChanges = ConfigChanges.Orientation
        | ConfigChanges.ScreenSize
        | ConfigChanges.UiMode
        | ConfigChanges.ScreenLayout
        | ConfigChanges.SmallestScreenSize)]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataSchemes = new[] { "content", "file" },
    DataMimeType = "video/*")]
public sealed class MainActivity : Activity
{
    private const int OpenVideoRequestCode = 4101;
    private const int SeekBarMaximum = 1000;
    private const int VolumeMaximum = 100;

    private readonly Handler _main = new(Looper.MainLooper!);
    private TextureView? _video;
    private MediaPlayer? _player;
    private Surface? _surface;
    private TextView? _status;
    private Button? _openButton;
    private Button? _playPauseButton;
    private SeekBar? _seekBar;
    private SeekBar? _volumeBar;
    private Button? _muteButton;
    private Action? _progressTick;
    private AndroidUri? _sessionUri;
    private int _durationMilliseconds;
    private float _volume = 1f;
    private float _volumeBeforeMute = 1f;
    private bool _muted;
    private bool _prepared;
    private bool _destroyed;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(BuildContent());
        HandleIncomingIntent(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        HandleIncomingIntent(intent);
    }

    protected override void OnActivityResult(
        int requestCode,
        Result resultCode,
        Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode != OpenVideoRequestCode
            || resultCode != Result.Ok
            || data?.Data is not { } uri
            || !IsSupportedVideoIntent(data))
        {
            return;
        }

        LoadVideo(uri);
    }

    protected override void OnPause()
    {
        PausePlayback();
        base.OnPause();
    }

    protected override void OnDestroy()
    {
        _destroyed = true;
        StopProgressUpdates();
        ReleasePlayback();
        _main.Dispose();
        base.OnDestroy();
    }

    private View BuildContent()
    {
        var root = new LinearLayout(this)
        {
            Orientation = WidgetOrientation.Vertical,
        };
        root.SetBackgroundColor(Color.Rgb(7, 21, 29));

        var stage = new FrameLayout(this);
        stage.SetBackgroundColor(Color.Black);
        root.AddView(
            stage,
            new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                0,
                1f));

        _video = new TextureView(this);
        _video.SurfaceTextureAvailable += OnSurfaceTextureAvailable;
        _video.SurfaceTextureDestroyed += OnSurfaceTextureDestroyed;
        stage.AddView(
            _video,
            new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.MatchParent));

        _status = new TextView(this)
        {
            Text = "Choose a video",
        };
        _status.SetTextColor(Color.White);
        _status.SetTextSize(ComplexUnitType.Sp, 16);
        _status.Gravity = GravityFlags.Center;
        _status.SetPadding(32, 16, 32, 16);
        _status.SetBackgroundColor(Color.Argb(210, 7, 21, 29));
        stage.AddView(
            _status,
            new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.WrapContent,
                ViewGroup.LayoutParams.WrapContent,
                GravityFlags.Center));

        _seekBar = new SeekBar(this)
        {
            Max = SeekBarMaximum,
            Progress = 0,
            Enabled = false,
        };
        _seekBar.ContentDescription = "Seek within video";
        _seekBar.ProgressChanged += OnSeekProgressChanged;
        root.AddView(
            _seekBar,
            new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent));

        var audioControls = new LinearLayout(this)
        {
            Orientation = WidgetOrientation.Horizontal,
        };
        audioControls.SetGravity(GravityFlags.CenterVertical);
        audioControls.SetPadding(12, 0, 12, 0);

        var volumeLabel = new TextView(this)
        {
            Text = "Volume",
        };
        volumeLabel.SetTextColor(Color.White);
        volumeLabel.SetTextSize(ComplexUnitType.Sp, 13);
        audioControls.AddView(
            volumeLabel,
            new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WrapContent,
                ViewGroup.LayoutParams.WrapContent));

        _volumeBar = new SeekBar(this)
        {
            Max = VolumeMaximum,
            Progress = VolumeMaximum,
            Enabled = false,
        };
        _volumeBar.ContentDescription = "Video volume";
        _volumeBar.ProgressChanged += OnVolumeProgressChanged;
        var volumeParameters = new LinearLayout.LayoutParams(
            0,
            ViewGroup.LayoutParams.WrapContent,
            1f);
        volumeParameters.LeftMargin = 8;
        audioControls.AddView(_volumeBar, volumeParameters);

        _muteButton = CreateButton("Mute", "Mute or unmute video audio");
        _muteButton.Enabled = false;
        _muteButton.Click += (_, _) => ToggleMute();
        var muteParameters = new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.WrapContent,
            ViewGroup.LayoutParams.WrapContent);
        muteParameters.LeftMargin = 8;
        audioControls.AddView(_muteButton, muteParameters);

        root.AddView(
            audioControls,
            new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent));

        var controls = new LinearLayout(this)
        {
            Orientation = WidgetOrientation.Horizontal,
        };
        controls.SetPadding(12, 4, 12, 8);

        _openButton = CreateButton("Open video", "Open a video");
        _openButton.Click += (_, _) => OpenVideoPicker();
        controls.AddView(
            _openButton,
            new LinearLayout.LayoutParams(
                0,
                ViewGroup.LayoutParams.WrapContent,
                1f));

        _playPauseButton = CreateButton("Play", "Play or pause the video");
        _playPauseButton.Enabled = false;
        _playPauseButton.Click += (_, _) => TogglePlayback();
        var playParameters = new LinearLayout.LayoutParams(
            0,
            ViewGroup.LayoutParams.WrapContent,
            1f);
        playParameters.LeftMargin = 8;
        controls.AddView(_playPauseButton, playParameters);

        root.AddView(
            controls,
            new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent));

        var privacyNote = new TextView(this)
        {
            Text = "Foreground only · no playback history",
        };
        privacyNote.SetTextColor(Color.Rgb(166, 184, 191));
        privacyNote.SetTextSize(ComplexUnitType.Sp, 12);
        privacyNote.Gravity = GravityFlags.Center;
        privacyNote.SetPadding(12, 0, 12, 12);
        root.AddView(
            privacyNote,
            new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent));

        return root;
    }

    private Button CreateButton(string text, string description)
    {
        var button = new Button(this)
        {
            Text = text,
        };
        button.ContentDescription = description;
        button.SetAllCaps(false);
        button.SetTextSize(ComplexUnitType.Sp, 14);
        button.SetMinHeight(48);
        return button;
    }

    private void OpenVideoPicker()
    {
        if (_destroyed)
            return;

        var intent = new Intent(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("video/*");
        intent.PutExtra(Intent.ExtraLocalOnly, true);
        StartActivityForResult(intent, OpenVideoRequestCode);
    }

    private void HandleIncomingIntent(Intent? intent)
    {
        if (intent?.Action != Intent.ActionView
            || intent.Data is not { } uri
            || !IsSupportedVideoIntent(intent))
        {
            return;
        }

        LoadVideo(uri);
    }

    private static bool IsSupportedVideoIntent(Intent intent)
    {
        var scheme = intent.Data?.Scheme;
        if (!string.Equals(scheme, "content", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(scheme, "file", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(intent.Type)
            || intent.Type.StartsWith("video/", StringComparison.OrdinalIgnoreCase);
    }

    private void LoadVideo(AndroidUri uri)
    {
        if (_video is null || _destroyed)
            return;

        ReleasePlayback();
        _sessionUri = uri;
        _prepared = false;
        _durationMilliseconds = 0;
        UpdateSeekBar(0);
        SetStatus("Loading video", visible: true);
        SetControls(enabled: false);
        TryStartPlayback();
    }

    private void TryStartPlayback()
    {
        if (_sessionUri is null
            || _surface is null
            || _player is not null
            || _destroyed)
        {
            return;
        }

        try
        {
            var player = new MediaPlayer();
            player.Prepared += (_, _) => HandlePrepared();
            player.Completion += (_, _) => HandleCompletion();
            player.Error += (_, _) => HandlePlaybackError();
            player.SetSurface(_surface);
            player.SetDataSource(this, _sessionUri);
            _player = player;
            ApplyAudioVolume();
            player.PrepareAsync();
        }
        catch
        {
            HandlePlaybackError();
        }
    }

    private void OnSurfaceTextureAvailable(
        object? sender,
        TextureView.SurfaceTextureAvailableEventArgs args)
    {
        if (_destroyed)
            return;

        try
        {
            _surface?.Release();
            _surface = new Surface(args.Surface);
            TryStartPlayback();
        }
        catch
        {
            HandlePlaybackError();
        }
    }

    private void OnSurfaceTextureDestroyed(
        object? sender,
        TextureView.SurfaceTextureDestroyedEventArgs args)
    {
        ReleasePlayback();
        _surface?.Release();
        _surface = null;
    }

    private void HandlePrepared()
    {
        if (_player is null || _destroyed || _sessionUri is null)
            return;

        try
        {
            _durationMilliseconds = Math.Max(0, _player.Duration);
            _prepared = true;
            SetControls(enabled: true);
            ApplyAudioVolume();
            _player.Start();
            _playPauseButton.Text = "Pause";
            SetStatus(string.Empty, visible: false);
            StartProgressUpdates();
        }
        catch
        {
            HandlePlaybackError();
        }
    }

    private void HandleCompletion()
    {
        if (_destroyed)
            return;

        StopProgressUpdates();
        UpdateSeekBar(SeekBarMaximum);
        if (_playPauseButton is not null)
        {
            _playPauseButton.Enabled = _prepared;
            _playPauseButton.Text = "Replay";
        }

        SetStatus("Finished", visible: true);
    }

    private void HandlePlaybackError()
    {
        if (_destroyed)
            return;

        ReleasePlayback();
        SetStatus("Unable to play this video", visible: true);
    }

    private void TogglePlayback()
    {
        if (_player is null || !_prepared)
            return;

        try
        {
            if (_player.IsPlaying)
            {
                PausePlayback();
                return;
            }

            if (_durationMilliseconds > 0 && _player.CurrentPosition >= _durationMilliseconds)
                _player.SeekTo(0);

            _player.Start();
            _playPauseButton!.Text = "Pause";
            SetStatus(string.Empty, visible: false);
            StartProgressUpdates();
        }
        catch
        {
            HandlePlaybackError();
        }
    }

    private void PausePlayback()
    {
        if (_player is null || !_prepared)
            return;

        try
        {
            if (_player.IsPlaying)
                _player.Pause();
        }
        catch
        {
            return;
        }

        StopProgressUpdates();
        if (_playPauseButton is not null)
            _playPauseButton.Text = "Play";
        SetStatus("Paused", visible: true);
    }

    private void ReleasePlayback()
    {
        StopProgressUpdates();
        if (_player is not null)
        {
            try
            {
                _player.Stop();
            }
            catch
            {
            }

            try
            {
                _player.Reset();
            }
            catch
            {
            }

            _player.Release();
            _player = null;
        }

        _sessionUri = null;
        _durationMilliseconds = 0;
        _prepared = false;
        UpdateSeekBar(0);
        SetControls(enabled: false);
    }

    private void OnSeekProgressChanged(object? sender, SeekBar.ProgressChangedEventArgs args)
    {
        if (!args.FromUser
            || _player is null
            || !_prepared
            || _durationMilliseconds <= 0)
        {
            return;
        }

        try
        {
            var position = (int)((long)_durationMilliseconds * args.Progress / SeekBarMaximum);
            _player.SeekTo(position);
        }
        catch
        {
        }
    }

    private void OnVolumeProgressChanged(
        object? sender,
        SeekBar.ProgressChangedEventArgs args)
    {
        if (!args.FromUser)
            return;

        _volume = Math.Clamp((float)args.Progress / VolumeMaximum, 0f, 1f);
        if (_volume > 0f)
            _volumeBeforeMute = _volume;
        ApplyAudioVolume();
    }

    private void ToggleMute()
    {
        if (_muted)
        {
            _muted = false;
            if (_volume <= 0f)
            {
                _volume = _volumeBeforeMute > 0f ? _volumeBeforeMute : 1f;
                _volumeBar!.Progress = (int)Math.Round(_volume * VolumeMaximum);
            }

            _muteButton!.Text = "Mute";
        }
        else
        {
            if (_volume > 0f)
                _volumeBeforeMute = _volume;

            _muted = true;
            _muteButton!.Text = "Unmute";
        }

        ApplyAudioVolume();
    }

    private void ApplyAudioVolume()
    {
        try
        {
            var volume = _muted ? 0f : _volume;
            _player?.SetVolume(volume, volume);
        }
        catch
        {
        }
    }

    private void StartProgressUpdates()
    {
        if (_progressTick is null)
            _progressTick = UpdateProgress;

        _main.RemoveCallbacks(_progressTick);
        _main.Post(_progressTick);
    }

    private void StopProgressUpdates()
    {
        if (_progressTick is not null)
            _main.RemoveCallbacks(_progressTick);
    }

    private void UpdateProgress()
    {
        if (_destroyed || _player is null || !_prepared)
            return;

        try
        {
            if (_player.IsPlaying && _durationMilliseconds > 0)
            {
                var position = Math.Clamp(_player.CurrentPosition, 0, _durationMilliseconds);
                UpdateSeekBar(
                    (int)((long)position * SeekBarMaximum / _durationMilliseconds));
                _main.PostDelayed(_progressTick!, 500);
            }
        }
        catch
        {
        }
    }

    private void UpdateSeekBar(int progress)
    {
        if (_seekBar is not null)
            _seekBar.Progress = Math.Clamp(progress, 0, SeekBarMaximum);
    }

    private void SetControls(bool enabled)
    {
        if (_playPauseButton is not null)
            _playPauseButton.Enabled = enabled;
        if (_seekBar is not null)
            _seekBar.Enabled = enabled && _durationMilliseconds > 0;
        if (_volumeBar is not null)
            _volumeBar.Enabled = enabled;
        if (_muteButton is not null)
            _muteButton.Enabled = enabled;
    }

    private void SetStatus(string text, bool visible)
    {
        if (_status is null)
            return;

        _status.Text = text;
        _status.Visibility = visible ? ViewStates.Visible : ViewStates.Gone;
    }
}
