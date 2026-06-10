using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using ConceptStudio.Core;

namespace ConceptStudio.Ui;

internal sealed class ConceptWorkspace : Grid
{
    private readonly ColumnDefinition _leftCol = new(280, GridUnitType.Pixel);
    private readonly ColumnDefinition _rightCol = new(320, GridUnitType.Pixel);
    private readonly ConceptSettingsStore _settings;

    public ConceptWorkspace(ConceptSettingsStore settings, Control leftRail, Control centerBody, Control rightRail)
    {
        _settings = settings;
        _leftCol.Width = new GridLength(_settings.Settings.LeftColumnPixels, GridUnitType.Pixel);
        _rightCol.Width = new GridLength(_settings.Settings.RightColumnPixels, GridUnitType.Pixel);

        ColumnDefinitions = new ColumnDefinitions
        {
            _leftCol,
            new ColumnDefinition(GridLength.Auto),
            new ColumnDefinition(1, GridUnitType.Star),
            new ColumnDefinition(GridLength.Auto),
            _rightCol,
        };

        var splitterLeft = CreateSplitter();
        splitterLeft.DragCompleted += (_, _) => PersistLayout();
        var splitterRight = CreateSplitter();
        splitterRight.DragCompleted += (_, _) => PersistLayout();

        Grid.SetColumn(leftRail, 0);
        Children.Add(leftRail);
        Grid.SetColumn(splitterLeft, 1);
        Children.Add(splitterLeft);
        Grid.SetColumn(centerBody, 2);
        Children.Add(centerBody);
        Grid.SetColumn(splitterRight, 3);
        Children.Add(splitterRight);
        Grid.SetColumn(rightRail, 4);
        Children.Add(rightRail);
    }

    private void PersistLayout()
    {
        _settings.Settings.LeftColumnPixels = _leftCol.ActualWidth;
        _settings.Settings.RightColumnPixels = _rightCol.ActualWidth;
        _settings.Save();
    }

    private static GridSplitter CreateSplitter() =>
        new()
        {
            Width = 4,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ResizeDirection = GridResizeDirection.Columns,
        };
}
