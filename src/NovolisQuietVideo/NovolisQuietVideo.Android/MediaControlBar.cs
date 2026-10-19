using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;

namespace NovolisQuietVideo.Android;

/// <summary>
/// A small, compositional transport control that can be reused by video or
/// audio surfaces without publishing media metadata to Android system UI.
/// </summary>
public sealed class MediaControlBar : LinearLayout
{
    private readonly ImageButton _previousButton;
    private readonly ImageButton _playPauseButton;
    private readonly ImageButton _nextButton;

    public MediaControlBar(Context context)
        : base(context)
    {
        Orientation = Orientation.Horizontal;
        SetGravity(GravityFlags.Center);
        SetPadding(12, 0, 12, 0);

        _previousButton = CreateButton(
            Resource.Drawable.media_previous,
            "Previous item");
        _previousButton.Click += (_, _) =>
            PreviousRequested?.Invoke(this, EventArgs.Empty);

        _playPauseButton = CreateButton(
            Resource.Drawable.media_play,
            "Play");
        _playPauseButton.Background = CreateAccentBackground();
        _playPauseButton.Click += (_, _) =>
            PlayPauseRequested?.Invoke(this, EventArgs.Empty);

        _nextButton = CreateButton(
            Resource.Drawable.media_next,
            "Next item");
        _nextButton.Click += (_, _) =>
            NextRequested?.Invoke(this, EventArgs.Empty);

        AddButton(_previousButton);
        AddButton(_playPauseButton);
        AddButton(_nextButton);
        SetTransportEnabled(false);
        SetNavigationEnabled(false, false);
    }

    public event EventHandler? PreviousRequested;

    public event EventHandler? PlayPauseRequested;

    public event EventHandler? NextRequested;

    public void SetTransportEnabled(bool enabled)
    {
        _playPauseButton.Enabled = enabled;
        _playPauseButton.Alpha = enabled ? 1f : 0.45f;
        if (!enabled)
            SetPlaying(false);
    }

    public void SetPlaying(bool isPlaying)
    {
        _playPauseButton.SetImageResource(
            isPlaying
                ? Resource.Drawable.media_pause
                : Resource.Drawable.media_play);
        _playPauseButton.ContentDescription = isPlaying ? "Pause" : "Play";
    }

    public void SetRetryAvailable(bool enabled)
    {
        _playPauseButton.Enabled = enabled;
        if (enabled)
        {
            _playPauseButton.SetImageResource(Resource.Drawable.media_play);
            _playPauseButton.ContentDescription = "Retry playback";
        }
        else
        {
            SetPlaying(false);
        }
    }

    public void SetNavigationEnabled(bool hasPrevious, bool hasNext)
    {
        _previousButton.Enabled = hasPrevious;
        _nextButton.Enabled = hasNext;
        _previousButton.Alpha = hasPrevious ? 1f : 0.45f;
        _nextButton.Alpha = hasNext ? 1f : 0.45f;
    }

    private ImageButton CreateButton(int iconResource, string description)
    {
        var button = new ImageButton(Context!)
        {
            ContentDescription = description,
        };
        button.SetImageResource(iconResource);
        button.Background = CreateControlBackground();
        button.SetPadding(16, 8, 16, 8);
        button.SetMinimumHeight(48);
        button.SetMinimumWidth(72);
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
        surface.SetCornerRadius(14f);
        surface.SetStroke(1, Color.Rgb(55, 83, 94));
        return surface;
    }

    private static StateListDrawable CreateAccentBackground()
    {
        var states = new StateListDrawable();
        states.AddState(
            new[] { -global::Android.Resource.Attribute.StateEnabled },
            CreateSurface(Color.Rgb(24, 37, 45)));
        states.AddState(
            new[] { global::Android.Resource.Attribute.StatePressed },
            CreateSurface(Color.Rgb(235, 157, 83)));
        states.AddState(
            Array.Empty<int>(),
            CreateSurface(Color.Rgb(198, 119, 57)));
        return states;
    }

    private void AddButton(ImageButton button)
    {
        AddView(
            button,
            new LinearLayout.LayoutParams(
                0,
                ViewGroup.LayoutParams.WrapContent,
                1f));
    }
}
