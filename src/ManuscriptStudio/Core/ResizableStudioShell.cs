using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace ManuscriptStudio.Core;

internal sealed class ResizableStudioShell : Grid
{
    private const double MinSideWidth = 120;

    private readonly ColumnDefinition _leftCol = new(280, GridUnitType.Pixel);
    private readonly ColumnDefinition _rightCol = new(420, GridUnitType.Pixel);
    private readonly ManuscriptSettingsStore _settings;

    public ResizableStudioShell(
        Control leftRail,
        Control center,
        Control rightRail,
        ManuscriptSettingsStore settings)
    {
        _settings = settings;
        var layout = settings.Settings.Layout;
        _leftCol.Width = new GridLength(Math.Max(MinSideWidth, layout.LeftColumnPixels), GridUnitType.Pixel);
        _rightCol.Width = new GridLength(Math.Max(MinSideWidth, layout.RightColumnPixels), GridUnitType.Pixel);

        ColumnDefinitions = new ColumnDefinitions
        {
            _leftCol,
            new ColumnDefinition(GridLength.Auto),
            new ColumnDefinition(1, GridUnitType.Star),
            new ColumnDefinition(GridLength.Auto),
            _rightCol,
        };

        var leftBorder = Border(leftRail, new Thickness(0, 0, 1, 0));
        var rightBorder = Border(rightRail, new Thickness(1, 0, 0, 0));

        var splitterLeft = CreateSplitter();
        splitterLeft.DragCompleted += (_, _) => PersistLayout();
        var splitterRight = CreateSplitter();
        splitterRight.DragCompleted += (_, _) => PersistLayout();

        Grid.SetColumn(leftBorder, 0);
        Children.Add(leftBorder);
        Grid.SetColumn(splitterLeft, 1);
        Children.Add(splitterLeft);
        Grid.SetColumn(center, 2);
        Children.Add(center);
        Grid.SetColumn(splitterRight, 3);
        Children.Add(splitterRight);
        Grid.SetColumn(rightBorder, 4);
        Children.Add(rightBorder);
    }

    private void PersistLayout()
    {
        if (_leftCol.Width.IsAbsolute)
            _settings.Settings.Layout.LeftColumnPixels = _leftCol.Width.Value;
        if (_rightCol.Width.IsAbsolute)
            _settings.Settings.Layout.RightColumnPixels = _rightCol.Width.Value;
        _settings.Save();
    }

    private static Border Border(Control child, Thickness borderThickness) =>
        new()
        {
            BorderBrush = Brushes.Gray,
            BorderThickness = borderThickness,
            Child = child,
        };

    private static GridSplitter CreateSplitter() =>
        new()
        {
            Width = 5,
            ResizeDirection = GridResizeDirection.Columns,
            Background = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)),
        };
}
