using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using FrankMoat.Game;
using FrankMoat.Input;
using FrankMoat.Narrative;
using Novolis.Avalonia.GraphicalProfile;

namespace FrankMoat;

internal sealed class FrankMoatHud : UserControl
{
    private readonly TextBlock _title = Text("FRANK MOAT", 18, FontWeight.Bold);
    private readonly TextBlock _status = Text(string.Empty, 14, FontWeight.Normal);
    private readonly TextBlock _objective = Text(string.Empty, 15, FontWeight.SemiBold);
    private readonly TextBlock _faith = Text(string.Empty, 14, FontWeight.Normal);
    private readonly TextBlock _interaction = Text(string.Empty, 14, FontWeight.Bold);
    private readonly TextBlock _controls = Text(
        "WASD move · mouse aim · LMB fire · R reload · E interact · C control mode",
        12,
        FontWeight.Normal);
    private readonly TextBlock _map = Text(string.Empty, 15, FontWeight.Normal);
    private readonly Border _mapPanel;
    private readonly TextBlock _recordingTitle = Text(string.Empty, 21, FontWeight.Bold);
    private readonly TextBlock _recordingBody = Text(string.Empty, 16, FontWeight.Normal);
    private readonly Border _recordingPanel;

    public FrankMoatHud()
    {
        IsHitTestVisible = false;
        var top = new Border
        {
            Background = GraphicalProfile.SurfaceBrush,
            BorderBrush = GraphicalProfile.BorderBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(14, 9),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                Children =
                {
                    _title,
                    _status,
                    _objective,
                },
            },
        };
        Grid.SetColumn(_status, 1);
        _status.HorizontalAlignment = HorizontalAlignment.Center;
        Grid.SetColumn(_objective, 2);
        _objective.HorizontalAlignment = HorizontalAlignment.Right;

        var faithPanel = new Border
        {
            Background = GraphicalProfile.RaisedBrush,
            BorderBrush = GraphicalProfile.BorderBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 8),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(14, 14, 14, 48),
            MaxWidth = 760,
            Child = _faith,
        };

        var bottom = new Border
        {
            Background = GraphicalProfile.SurfaceBrush,
            BorderBrush = GraphicalProfile.BorderBrush,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(14, 7),
            VerticalAlignment = VerticalAlignment.Bottom,
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    _interaction,
                    _controls,
                },
            },
        };
        _interaction.Foreground = GraphicalProfile.AccentFillBrush;

        _mapPanel = new Border
        {
            Background = GraphicalProfile.BackgroundBrush,
            BorderBrush = GraphicalProfile.AccentFillBrush,
            BorderThickness = new Thickness(2),
            Padding = new Thickness(18),
            Width = 780,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = false,
            Child = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    Text("FACILITY MAP", 22, FontWeight.Bold),
                    _map,
                    Text("Tab closes the map", 12, FontWeight.Normal),
                },
            },
        };

        _recordingPanel = new Border
        {
            Background = GraphicalProfile.BackgroundBrush,
            BorderBrush = GraphicalProfile.AccentFillBrush,
            BorderThickness = new Thickness(2),
            Padding = new Thickness(22),
            Width = 760,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = false,
            Child = new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    _recordingTitle,
                    _recordingBody,
                    Text("E / Enter closes the recording", 12, FontWeight.Normal),
                },
            },
        };

        Content = new Grid
        {
            Children =
            {
                top,
                faithPanel,
                bottom,
                _mapPanel,
                _recordingPanel,
            },
        };
    }

    public void Refresh(
        FrankMoatWorld world,
        CampaignState campaign,
        string status,
        FounderRecording? recording,
        ControlMode controlMode)
    {
        _status.Text =
            $"{world.Floor.Name}   ·   HP {world.Frank.Health}  ARM {world.Frank.Armor}   ·   {world.Weapon.Spec.Name} {world.Weapon.RoundsInGun}/{world.Weapon.Reserve}";
        _objective.Text = world.ObjectiveText(campaign);
        _faith.Text = world.Faith.Text.Length == 0
            ? status
            : $"{world.Faith.Speaker}  {world.Faith.Text}";
        var interaction = world.NearestInteraction(campaign);
        _interaction.Text = interaction is null
            ? string.Empty
            : interaction.IsInRange(world.Frank.Position)
                ? interaction.PromptText
                : $"Approach {interaction.Label}";
        _controls.Text =
            $"{controlMode switch
            {
                ControlMode.MouseAim => "WASD move · mouse aim",
                _ => "WASD move · arrow-key aim",
            }} · LMB fire · R reload · E interact · C switch aim · Tab map · Esc pause";
        _map.Text = world.MapText();
        _mapPanel.IsVisible = world.MapVisible && recording is null;
        _recordingPanel.IsVisible = recording is not null;
        if (recording is not null)
        {
            _recordingTitle.Text = recording.Title;
            _recordingBody.Text = string.Join(Environment.NewLine + Environment.NewLine, recording.Lines);
        }
    }

    private static TextBlock Text(string content, double size, FontWeight weight) => new()
    {
        Text = content,
        FontSize = size,
        FontWeight = weight,
        Foreground = GraphicalProfile.TextBrush,
        TextWrapping = TextWrapping.Wrap,
    };
}
