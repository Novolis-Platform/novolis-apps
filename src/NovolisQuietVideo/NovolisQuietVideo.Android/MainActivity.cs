using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Provider;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidUri = Android.Net.Uri;
using DeviceConfiguration = global::Android.Content.Res.Configuration;
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
    ScreenOrientation = ScreenOrientation.FullSensor,
    ConfigurationChanges = ConfigChanges.Orientation
        | ConfigChanges.ScreenSize
        | ConfigChanges.UiMode
        | ConfigChanges.ScreenLayout
        | ConfigChanges.SmallestScreenSize)]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataSchemes = new[] { "content", "file", "https" },
    DataMimeType = "video/*")]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataSchemes = new[] { "content", "file", "https" },
    DataMimeType = "application/octet-stream")]
public sealed class MainActivity : Activity
{
    private const int OpenVideoRequestCode = 4101;
    private const int OpenFolderRequestCode = 4102;
    private const int SeekBarMaximum = 1000;
    private const int VolumeMaximum = 100;
    private const float MinimumZoom = 1f;
    private const float MaximumZoom = 4f;
    private const int MaximumPrepareRetries = 10;
    private const int PrepareRetryDelayMilliseconds = 1000;
    private const string PreferencesName = "quiet_video_preferences";
    private const string VolumePreferenceKey = "volume";
    private const string VolumeBeforeMutePreferenceKey = "volume_before_mute";
    private const string MutedPreferenceKey = "muted";

    private readonly Handler _main = new(Looper.MainLooper!);
    private readonly Matrix _videoTransform = new();
    private TextureView? _video;
    private MediaPlayer? _player;
    private Surface? _surface;
    private TextView? _status;
    private Button? _openButton;
    private Button? _openFolderButton;
    private MediaControlBar? _transportControls;
    private Button? _viewModeButton;
    private SeekBar? _seekBar;
    private SeekBar? _volumeBar;
    private ImageButton? _muteButton;
    private Action? _progressTick;
    private AndroidUri? _sessionUri;
    private readonly List<AndroidUri> _folderVideos = [];
    private int _folderIndex = -1;
    private int _folderLoadVersion;
    private int _durationMilliseconds;
    private int _videoWidth;
    private int _videoHeight;
    private float _volume = 1f;
    private float _volumeBeforeMute = 1f;
    private float _zoom = MinimumZoom;
    private bool _muted;
    private bool _shouldPlay = true;
    private bool _prepared;
    private bool _destroyed;
    private VideoScaleMode _scaleMode = VideoScaleMode.FitView;
    private bool _pinching;
    private float _pinchStartDistance;
    private float _pinchStartZoom = MinimumZoom;
    private Action? _prepareTimeout;
    private Action? _prepareRetry;
    private int _prepareRetryCount;
    private ISharedPreferences? _preferences;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        LoadAudioPreferences();
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
        if (requestCode == OpenVideoRequestCode)
        {
            if (resultCode == Result.Ok
                && data?.Data is { } uri
                && IsSupportedVideoIntent(data))
            {
                LoadVideo(uri);
            }

            return;
        }

        if (requestCode == OpenFolderRequestCode
            && resultCode == Result.Ok
            && data?.Data is { } treeUri)
        {
            LoadFolder(treeUri);
        }
    }

    protected override void OnPause()
    {
        _shouldPlay = false;
        PausePlayback();
        base.OnPause();
    }

    protected override void OnDestroy()
    {
        _destroyed = true;
        _folderLoadVersion++;
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
        _video.ContentDescription = "Video playback surface; pinch to zoom";
        _video.SurfaceTextureAvailable += OnSurfaceTextureAvailable;
        _video.SurfaceTextureDestroyed += OnSurfaceTextureDestroyed;
        _video.Touch += OnVideoTouch;
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

        _transportControls = new MediaControlBar(this);
        _transportControls.PreviousRequested += (_, _) => SkipFolderVideo(-1);
        _transportControls.PlayPauseRequested += (_, _) => TogglePlayback();
        _transportControls.NextRequested += (_, _) => SkipFolderVideo(1);
        root.AddView(
            _transportControls,
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
            Progress = (int)Math.Round(_volume * VolumeMaximum),
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

        _muteButton = CreateIconButton(
            _muted
                ? Resource.Drawable.media_mute
                : Resource.Drawable.media_volume,
            _muted ? "Unmute video audio" : "Mute video audio");
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
        _openButton.SetCompoundDrawablesWithIntrinsicBounds(
            Resource.Drawable.media_open,
            0,
            0,
            0);
        _openButton.CompoundDrawablePadding = 8;
        _openButton.Click += (_, _) => OpenVideoPicker();
        controls.AddView(
            _openButton,
            new LinearLayout.LayoutParams(
                0,
                ViewGroup.LayoutParams.WrapContent,
                1f));

        _openFolderButton = CreateButton("Open folder", "Open a folder of videos");
        _openFolderButton.SetCompoundDrawablesWithIntrinsicBounds(
            Resource.Drawable.media_folder,
            0,
            0,
            0);
        _openFolderButton.CompoundDrawablePadding = 8;
        _openFolderButton.Click += (_, _) => OpenFolderPicker();
        var folderParameters = new LinearLayout.LayoutParams(
            0,
            ViewGroup.LayoutParams.WrapContent,
            1f);
        folderParameters.LeftMargin = 8;
        controls.AddView(_openFolderButton, folderParameters);

        _viewModeButton = CreateButton(
            "Fit view",
            "Cycle video sizing between fit view, fit width, and fit height");
        _viewModeButton.SetCompoundDrawablesWithIntrinsicBounds(
            Resource.Drawable.media_aspect,
            0,
            0,
            0);
        _viewModeButton.CompoundDrawablePadding = 8;
        _viewModeButton.Click += (_, _) => CycleScaleMode();
        var viewModeParameters = new LinearLayout.LayoutParams(
            0,
            ViewGroup.LayoutParams.WrapContent,
            1f);
        viewModeParameters.LeftMargin = 8;
        controls.AddView(_viewModeButton, viewModeParameters);

        root.AddView(
            controls,
            new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent));

        var privacyNote = new TextView(this)
        {
            Text = "Foreground only · no playback history · pinch to zoom",
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

    public override void OnConfigurationChanged(DeviceConfiguration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        _video?.Post(() => ApplyZoom());
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
        button.SetTextColor(Color.Rgb(235, 241, 243));
        button.Background = CreateControlBackground();
        button.SetPadding(12, 0, 12, 0);
        button.SetMinHeight(48);
        return button;
    }

    private ImageButton CreateIconButton(int iconResource, string description)
    {
        var button = new ImageButton(this)
        {
            ContentDescription = description,
        };
        button.SetImageResource(iconResource);
        button.Background = CreateControlBackground();
        button.SetPadding(14, 8, 14, 8);
        button.SetMinimumHeight(48);
        button.SetMinimumWidth(64);
        return button;
    }

    private static StateListDrawable CreateControlBackground()
    {
        var states = new StateListDrawable();
        states.AddState(
            new[] { -global::Android.Resource.Attribute.StateEnabled },
            CreateSurface(Color.Rgb(24, 37, 45)));
        states.AddState(
            new[] { global::Android.Resource.Attribute.StatePressed },
            CreateSurface(Color.Rgb(183, 112, 54)));
        states.AddState(
            Array.Empty<int>(),
            CreateSurface(Color.Rgb(17, 45, 57)));
        return states;
    }

    private static GradientDrawable CreateSurface(Color color)
    {
        var surface = new GradientDrawable();
        surface.SetColor(color);
        surface.SetCornerRadius(10f);
        surface.SetStroke(1, Color.Rgb(55, 83, 94));
        return surface;
    }

    private void LoadAudioPreferences()
    {
        if (GetSharedPreferences(PreferencesName, FileCreationMode.Private)
            is not { } preferences)
        {
            return;
        }

        _preferences = preferences;
        _volume = Math.Clamp(
            preferences.GetFloat(VolumePreferenceKey, 1f),
            0f,
            1f);
        _volumeBeforeMute = Math.Clamp(
            preferences.GetFloat(VolumeBeforeMutePreferenceKey, _volume),
            0f,
            1f);
        if (_volumeBeforeMute <= 0f)
            _volumeBeforeMute = 1f;

        _muted = preferences.GetBoolean(MutedPreferenceKey, false);
    }

    private void SaveAudioPreferences()
    {
        try
        {
            if (_preferences is not { } preferences)
                return;

            if (preferences.Edit() is not { } editor)
                return;

            editor.PutFloat(VolumePreferenceKey, _volume);
            editor.PutFloat(VolumeBeforeMutePreferenceKey, _volumeBeforeMute);
            editor.PutBoolean(MutedPreferenceKey, _muted);
            editor.Apply();
        }
        catch
        {
        }
    }

    private void OpenVideoPicker()
    {
        if (_destroyed)
            return;

        var intent = new Intent(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("*/*");
        intent.PutExtra(Intent.ExtraMimeTypes, new[] { "video/*", "application/octet-stream" });
        StartActivityForResult(intent, OpenVideoRequestCode);
    }

    private void OpenFolderPicker()
    {
        if (_destroyed)
            return;

        var intent = new Intent(Intent.ActionOpenDocumentTree);
        intent.AddFlags(
            ActivityFlags.GrantReadUriPermission
            | ActivityFlags.GrantPrefixUriPermission);
        StartActivityForResult(intent, OpenFolderRequestCode);
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
            if (!string.Equals(scheme, "https", StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private void LoadVideo(AndroidUri uri, bool keepFolder = false)
    {
        if (_video is null || _destroyed)
            return;

        if (!keepFolder)
        {
            _folderLoadVersion++;
            _folderVideos.Clear();
            _folderIndex = -1;
        }

        ReleasePlayback();
        _sessionUri = uri;
        _prepareRetryCount = 0;
        _shouldPlay = true;
        _prepared = false;
        _durationMilliseconds = 0;
        UpdateSeekBar(0);
        UpdateFolderControls();
        SetStatus("Loading video", visible: true);
        SetControls(enabled: false);
        TryStartPlayback();
    }

    private void LoadFolder(AndroidUri treeUri)
    {
        if (_destroyed)
            return;

        var loadVersion = ++_folderLoadVersion;
        _folderVideos.Clear();
        _folderIndex = -1;
        ReleasePlayback();
        UpdateFolderControls();
        SetStatus("Loading folder", visible: true);
        SetControls(enabled: false);
        _ = LoadFolderAsync(treeUri, loadVersion);
    }

    private async Task LoadFolderAsync(AndroidUri treeUri, int loadVersion)
    {
        try
        {
            var videos = await Task.Run(
                () => QueryFolderVideos(treeUri)).ConfigureAwait(false);
            TryPostToMain(() => HandleFolderLoaded(videos, loadVersion));
        }
        catch
        {
            TryPostToMain(() => HandleFolderLoadFailure(loadVersion));
        }
    }

    private List<AndroidUri> QueryFolderVideos(AndroidUri treeUri)
    {
        var resolver = ContentResolver
            ?? throw new InvalidOperationException("Android content resolver unavailable.");
        var parentDocumentId = DocumentsContract.GetTreeDocumentId(treeUri)
            ?? throw new InvalidOperationException("Folder document id unavailable.");
        var childrenUri = DocumentsContract.BuildChildDocumentsUriUsingTree(
            treeUri,
            parentDocumentId)
            ?? throw new InvalidOperationException("Folder children unavailable.");
        using var cursor = resolver.Query(
            childrenUri,
            [
                DocumentsContract.Document.ColumnDocumentId,
                DocumentsContract.Document.ColumnMimeType,
            ],
            null,
            null,
            null);
        if (cursor is null)
            return [];

        var idColumn = cursor.GetColumnIndex(
            DocumentsContract.Document.ColumnDocumentId);
        var mimeColumn = cursor.GetColumnIndex(
            DocumentsContract.Document.ColumnMimeType);
        if (idColumn < 0 || mimeColumn < 0)
            return [];

        var videos = new List<AndroidUri>();
        while (cursor.MoveToNext())
        {
            var documentId = cursor.GetString(idColumn);
            var mimeType = cursor.GetString(mimeColumn);
            if (string.IsNullOrWhiteSpace(documentId)
                || string.Equals(
                    mimeType,
                    DocumentsContract.Document.MimeTypeDir,
                    StringComparison.Ordinal)
                || (!IsVideoMimeType(mimeType)
                    && !string.Equals(
                        mimeType,
                        "application/octet-stream",
                        StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var documentUri = DocumentsContract.BuildDocumentUriUsingTree(
                treeUri,
                documentId);
            if (documentUri is not null)
                videos.Add(documentUri);
        }

        return videos;
    }

    private void HandleFolderLoaded(List<AndroidUri> videos, int loadVersion)
    {
        if (_destroyed || loadVersion != _folderLoadVersion)
            return;

        _folderVideos.AddRange(videos);
        if (_folderVideos.Count == 0)
        {
            UpdateFolderControls();
            SetStatus("No videos in folder", visible: true);
            return;
        }

        _folderIndex = 0;
        UpdateFolderControls();
        LoadFolderVideo(_folderIndex);
    }

    private void HandleFolderLoadFailure(int loadVersion)
    {
        if (_destroyed || loadVersion != _folderLoadVersion)
            return;

        _folderVideos.Clear();
        _folderIndex = -1;
        UpdateFolderControls();
        SetStatus("Unable to open this folder", visible: true);
    }

    private void LoadFolderVideo(int index)
    {
        if (index < 0 || index >= _folderVideos.Count)
            return;

        _folderIndex = index;
        LoadVideo(_folderVideos[index], keepFolder: true);
    }

    private void SkipFolderVideo(int direction)
    {
        var target = _folderIndex + direction;
        if (target >= 0 && target < _folderVideos.Count)
            LoadFolderVideo(target);
    }

    private bool HasNextFolderVideo =>
        _folderIndex >= 0 && _folderIndex + 1 < _folderVideos.Count;

    private static bool IsVideoMimeType(string? mimeType) =>
        !string.IsNullOrWhiteSpace(mimeType)
        && mimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);

    private void UpdateFolderControls()
    {
        _transportControls?.SetNavigationEnabled(
            _folderIndex > 0,
            HasNextFolderVideo);
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

        MediaPlayer? player = null;
        try
        {
            var uri = _sessionUri;
            var surface = _surface;
            player = new MediaPlayer();
            _player = player;
            player.Completion += (_, _) =>
            {
                if (ReferenceEquals(_player, player))
                    TryPostToMain(HandleCompletion);
            };
            player.Error += (_, _) =>
            {
                if (ReferenceEquals(_player, player))
                {
                    TryPostToMain(
                        () =>
                        {
                            if (_prepared)
                                HandlePlaybackError(player);
                            else
                                HandlePrepareFailure(player);
                        });
                }
            };
            player.SetSurface(surface);
            ApplyAudioVolume();
            StartPrepareTimeout();
            _ = PreparePlayerAsync(player, uri);
        }
        catch
        {
            if (player is not null)
                HandlePrepareFailure(player);
            else
                HandlePlaybackError();
        }
    }

    private async Task PreparePlayerAsync(MediaPlayer player, AndroidUri uri)
    {
        try
        {
            await Task.Run(() =>
            {
                player.SetDataSource(this, uri);
                player.Prepare();
            }).ConfigureAwait(false);

            if (!TryPostToMain(() => HandlePrepared(player)))
                ReleasePlayer(player);
        }
        catch
        {
            if (!TryPostToMain(() => HandlePrepareFailure(player)))
                ReleasePlayer(player);
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
            ApplyZoom();
            if (_sessionUri is not null)
                SetStatus("Loading video", visible: true);
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
        try
        {
            _shouldPlay = _player?.IsPlaying == true;
        }
        catch
        {
            _shouldPlay = false;
        }

        ReleasePlayback(clearSession: false);
        _surface?.Release();
        _surface = null;
    }

    private void OnVideoTouch(object? sender, View.TouchEventArgs args)
    {
        if (args.Event is { } motionEvent)
        {
            switch (motionEvent.ActionMasked)
            {
                case MotionEventActions.Down:
                    _pinching = false;
                    _pinchStartDistance = 0f;
                    _pinchStartZoom = _zoom;
                    break;
                case MotionEventActions.PointerDown:
                    if (motionEvent.PointerCount >= 2)
                    {
                        _pinchStartDistance = GetPointerDistance(motionEvent);
                        _pinchStartZoom = _zoom;
                        _pinching = _pinchStartDistance > 0f;
                    }

                    break;
                case MotionEventActions.Move:
                    if (_pinching && motionEvent.PointerCount >= 2)
                    {
                        var distance = GetPointerDistance(motionEvent);
                        if (distance > 0f && _pinchStartDistance > 0f)
                        {
                            SetZoom(
                                _pinchStartZoom * distance / _pinchStartDistance);
                        }
                    }

                    break;
                case MotionEventActions.PointerUp:
                case MotionEventActions.Up:
                case MotionEventActions.Cancel:
                    _pinching = false;
                    _pinchStartDistance = 0f;
                    break;
            }
        }

        args.Handled = true;
    }

    private static float GetPointerDistance(MotionEvent motionEvent)
    {
        var deltaX = motionEvent.GetX(0) - motionEvent.GetX(1);
        var deltaY = motionEvent.GetY(0) - motionEvent.GetY(1);
        return (float)Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
    }

    private void HandlePrepared(MediaPlayer player)
    {
        if (!ReferenceEquals(_player, player)
            || _destroyed
            || _sessionUri is null)
        {
            ReleasePlayer(player);
            return;
        }

        try
        {
            _durationMilliseconds = Math.Max(0, player.Duration);
            _videoWidth = Math.Max(0, player.VideoWidth);
            _videoHeight = Math.Max(0, player.VideoHeight);
            ApplyZoom();
            StopPrepareTimeout();
            StopPrepareRetry();
            _prepareRetryCount = 0;
            _prepared = true;
            SetControls(enabled: true);
            ApplyAudioVolume();
            if (_shouldPlay)
            {
                player.Start();
                _transportControls?.SetPlaying(true);
                SetStatus(string.Empty, visible: false);
                StartProgressUpdates();
            }
            else
            {
                _transportControls?.SetPlaying(false);
                SetStatus("Paused", visible: true);
            }
        }
        catch
        {
            HandlePlaybackError();
        }
    }

    private void HandlePrepareFailure(MediaPlayer player)
    {
        if (!ReferenceEquals(_player, player))
        {
            ReleasePlayer(player);
            return;
        }

        if (_destroyed)
        {
            ReleasePlayback();
            return;
        }

        StopPrepareTimeout();
        ReleasePlayback(clearSession: false);
        if (_sessionUri is not null && _prepareRetryCount < MaximumPrepareRetries)
        {
            _prepareRetryCount++;
            _transportControls?.SetRetryAvailable(true);

            SetStatus("Waiting for more video data", visible: true);
            StartPrepareRetry();
            return;
        }

        ShowPlaybackRetryState();
    }

    private void HandleCompletion()
    {
        if (_destroyed)
            return;

        if (HasNextFolderVideo)
        {
            LoadFolderVideo(_folderIndex + 1);
            return;
        }

        _shouldPlay = false;
        StopProgressUpdates();
        UpdateSeekBar(SeekBarMaximum);
        _transportControls?.SetPlaying(false);
        _transportControls?.SetTransportEnabled(_prepared);
        UpdateFolderControls();

        SetStatus("Finished", visible: true);
    }

    private void HandlePlaybackError(MediaPlayer? source = null)
    {
        if (source is not null && !ReferenceEquals(_player, source))
        {
            ReleasePlayer(source);
            return;
        }

        if (_destroyed)
            return;

        StopPrepareTimeout();
        StopPrepareRetry();
        ReleasePlayback(clearSession: false);
        ShowPlaybackRetryState();
    }

    private void ShowPlaybackRetryState()
    {
        _transportControls?.SetRetryAvailable(_sessionUri is not null);

        SetStatus("Unable to play this video or stream", visible: true);
    }

    private void TogglePlayback()
    {
        if (_player is null || !_prepared)
        {
            if (_sessionUri is not null)
            {
                StopPrepareRetry();
                _prepareRetryCount = 0;
                _shouldPlay = true;
                SetStatus("Loading video", visible: true);
                SetControls(enabled: false);
                TryStartPlayback();
            }

            return;
        }

        try
        {
            if (_player.IsPlaying)
            {
                PausePlayback();
                return;
            }

            if (_durationMilliseconds > 0 && _player.CurrentPosition >= _durationMilliseconds)
                _player.SeekTo(0);

            _shouldPlay = true;
            _player.Start();
            _transportControls?.SetPlaying(true);
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
        _shouldPlay = false;
        _transportControls?.SetPlaying(false);
        SetStatus("Paused", visible: true);
    }

    private void ReleasePlayback(bool clearSession = true)
    {
        StopPrepareTimeout();
        StopPrepareRetry();
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

        if (clearSession)
        {
            _sessionUri = null;
            _prepareRetryCount = 0;
        }
        _durationMilliseconds = 0;
        _videoWidth = 0;
        _videoHeight = 0;
        _prepared = false;
        UpdateSeekBar(0);
        SetControls(enabled: false);
    }

    private void StartPrepareTimeout()
    {
        if (_prepareTimeout is null)
        {
            _prepareTimeout = () =>
            {
                if (!_prepared && _player is not null)
                    HandlePrepareFailure(_player);
            };
        }

        _main.RemoveCallbacks(_prepareTimeout);
        _main.PostDelayed(_prepareTimeout, 15_000);
    }

    private void StopPrepareTimeout()
    {
        if (_prepareTimeout is not null)
            _main.RemoveCallbacks(_prepareTimeout);
    }

    private void StartPrepareRetry()
    {
        if (_prepareRetry is null)
        {
            _prepareRetry = () =>
            {
                if (_destroyed || _sessionUri is null || _player is not null)
                    return;

                SetStatus("Loading video", visible: true);
                TryStartPlayback();
            };
        }

        _main.RemoveCallbacks(_prepareRetry);
        _main.PostDelayed(_prepareRetry, PrepareRetryDelayMilliseconds);
    }

    private void StopPrepareRetry()
    {
        if (_prepareRetry is not null)
            _main.RemoveCallbacks(_prepareRetry);
    }

    private bool TryPostToMain(Action action)
    {
        try
        {
            _main.Post(action);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void ReleasePlayer(MediaPlayer player)
    {
        try
        {
            player.Release();
        }
        catch
        {
        }
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
        {
            _volumeBeforeMute = _volume;
            _muted = false;
        }
        else
        {
            _muted = true;
        }

        UpdateMuteButton();
        SaveAudioPreferences();
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

        }
        else
        {
            if (_volume > 0f)
                _volumeBeforeMute = _volume;

            _muted = true;
        }

        UpdateMuteButton();
        SaveAudioPreferences();
        ApplyAudioVolume();
    }

    private void UpdateMuteButton()
    {
        if (_muteButton is null)
            return;

        _muteButton.SetImageResource(
            _muted
                ? Resource.Drawable.media_mute
                : Resource.Drawable.media_volume);
        _muteButton.ContentDescription =
            _muted ? "Unmute video audio" : "Mute video audio";
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

    private void SetZoom(float zoom)
    {
        _zoom = Math.Clamp(zoom, MinimumZoom, MaximumZoom);
        ApplyZoom();
    }

    private void CycleScaleMode()
    {
        _scaleMode = _scaleMode switch
        {
            VideoScaleMode.FitView => VideoScaleMode.FitWidth,
            VideoScaleMode.FitWidth => VideoScaleMode.FitHeight,
            _ => VideoScaleMode.FitView,
        };
        _zoom = MinimumZoom;
        UpdateViewModeButton();
        ApplyZoom();
    }

    private void UpdateViewModeButton()
    {
        if (_viewModeButton is null)
            return;

        var label = _scaleMode switch
        {
            VideoScaleMode.FitWidth => "Fit width",
            VideoScaleMode.FitHeight => "Fit height",
            _ => "Fit view",
        };
        _viewModeButton.Text = label;
        _viewModeButton.ContentDescription =
            $"Video sizing: {label}; tap to change";
    }

    private void ApplyZoom()
    {
        if (_video is null || _video.Width <= 0 || _video.Height <= 0)
            return;

        var viewWidth = _video.Width;
        var viewHeight = _video.Height;
        var scaleX = 1f;
        var scaleY = 1f;
        if (_videoWidth > 0 && _videoHeight > 0)
        {
            var videoAspect = (float)_videoWidth / _videoHeight;
            var viewAspect = (float)viewWidth / viewHeight;
            var targetWidth = (float)viewWidth;
            var targetHeight = (float)viewHeight;

            switch (_scaleMode)
            {
                case VideoScaleMode.FitWidth:
                    targetHeight = viewWidth / videoAspect;
                    break;
                case VideoScaleMode.FitHeight:
                    targetWidth = viewHeight * videoAspect;
                    break;
                default:
                    if (viewAspect > videoAspect)
                        targetWidth = viewHeight * videoAspect;
                    else
                        targetHeight = viewWidth / videoAspect;
                    break;
            }

            scaleX = targetWidth / viewWidth;
            scaleY = targetHeight / viewHeight;
        }

        _videoTransform.Reset();
        _videoTransform.SetScale(
            scaleX * _zoom,
            scaleY * _zoom,
            viewWidth / 2f,
            viewHeight / 2f);
        _video.SetTransform(_videoTransform);
        _video.Invalidate();
        _video.PostInvalidateOnAnimation();
        if (_video.Parent is View parent)
            parent.Invalidate();
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
        _transportControls?.SetTransportEnabled(enabled);
        if (_seekBar is not null)
            _seekBar.Enabled = enabled && _durationMilliseconds > 0;
        var sessionControlsEnabled = _sessionUri is not null;
        if (_volumeBar is not null)
            _volumeBar.Enabled = sessionControlsEnabled;
        if (_muteButton is not null)
            _muteButton.Enabled = sessionControlsEnabled;
        UpdateFolderControls();
    }

    private void SetStatus(string text, bool visible)
    {
        if (_status is null)
            return;

        _status.Text = text;
        _status.Visibility = visible ? ViewStates.Visible : ViewStates.Gone;
    }

    private enum VideoScaleMode
    {
        FitView,
        FitWidth,
        FitHeight,
    }

}
