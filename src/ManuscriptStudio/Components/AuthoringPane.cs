using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Novolis.Avalonia.Studio;

namespace ManuscriptStudio.Components;

/// <summary>Vertical stack of an action bar and a stretch body host.</summary>
internal sealed class AuthoringPane : Grid
{
    private readonly Grid _bodyHost = new();

    public AuthoringPane()
    {
        RowDefinitions = new RowDefinitions("Auto,*");

        ActionBar = StudioWorkspace.CreateToolbarRow();
        ActionBar.Margin = new Thickness(4, 4, 4, 0);

        Children.Add(ActionBar);
        Grid.SetRow(_bodyHost, 1);
        Children.Add(_bodyHost);
    }

    /// <summary>Toolbar row for buttons and dropdowns above the pane body.</summary>
    public StackPanel ActionBar { get; }

    /// <summary>Replaces the pane body content.</summary>
    public void SetBody(Control content)
    {
        _bodyHost.Children.Clear();
        content.HorizontalAlignment = HorizontalAlignment.Stretch;
        content.VerticalAlignment = VerticalAlignment.Stretch;
        _bodyHost.Children.Add(content);
    }
}
