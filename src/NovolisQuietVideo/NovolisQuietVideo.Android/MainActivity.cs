using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidUri = Android.Net.Uri;

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

    private readonly Handler _main = new(Looper.MainLooper!);
    private VideoView? _video;
    private TextView? _status;
    private Button? _openButton;
    private Button? _playPauseButton;
    private SeekBar? _seekBar;
    private Action? _progressTick;
    private AndroidUri? _sessionUri;
    private int _durationMilliseconds;
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
            Orientation = Orientation.Vertical,
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

        _video = new VideoView(this);
        _video.SetBackgroundColor(Color.Black);
        _video.Prepared += (_, _) => HandlePrepared();
        _video.Completion += (_, _) => HandleCompletion();
        _video.Error += (_, _) => HandlePlaybackError();
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

        var controls = new LinearLayout(this)
        {
            Orientation = Orientation.Horizontal,
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

        try
        {
            _video.SetVideoURI(uri);
        }
        catch
        {
            HandlePlaybackError();
        }
    }

    private void HandlePrepared()
    {
        if (_video is null || _destroyed || _sessionUri is null)
            return;

        try
        {
            _durationMilliseconds = Math.Max(0, _video.Duration);
            _prepared = true;
            _seekBar!.Enabled = _durationMilliseconds > 0;
            _playPauseButton!.Enabled = true;
            _video.Start();
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
        if (_video is null || !_prepared)
            return;

        try
        {
            if (_video.IsPlaying)
            {
                PausePlayback();
                return;
            }

            if (_durationMilliseconds > 0 && _video.CurrentPosition >= _durationMilliseconds)
                _video.SeekTo(0);

            _video.Start();
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
        if (_video is null || !_prepared)
            return;

        try
        {
            if (_video.IsPlaying)
                _video.Pause();
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
        if (_video is not null)
        {
            try
            {
                _video.StopPlayback();
            }
            catch
            {
            }
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
            || _video is null
            || !_prepared
            || _durationMilliseconds <= 0)
        {
            return;
        }

        try
        {
            var position = (int)((long)_durationMilliseconds * args.Progress / SeekBarMaximum);
            _video.SeekTo(position);
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
        if (_destroyed || _video is null || !_prepared)
            return;

        try
        {
            if (_video.IsPlaying && _durationMilliseconds > 0)
            {
                var position = Math.Clamp(_video.CurrentPosition, 0, _durationMilliseconds);
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
    }

    private void SetStatus(string text, bool visible)
    {
        if (_status is null)
            return;

        _status.Text = text;
        _status.Visibility = visible ? ViewStates.Visible : ViewStates.Gone;
    }
}
