using Microsoft.Maui.Controls.Handlers.Items;
using Microsoft.Maui.Handlers;
using Novolis.Maui.PdfViewer;

namespace NovolisPdfReader;

/// <summary>Pushes MAUI chrome onto WinUI so buttons and page cells keep their intended size and fill.</summary>
internal static class WinUiButtonChrome
{
    private const string PaperItemsTag = "novolis-paper-items";
    /// <summary>Maps MAUI button and CollectionView chrome onto native WinUI controls.</summary>
    public static void Map(IMauiHandlersCollection handlers)
    {
        ButtonHandler.Mapper.AppendToMapping(nameof(VisualElement.Background), ApplyButton);
        ButtonHandler.Mapper.AppendToMapping(nameof(Button.TextColor), ApplyButton);
        ButtonHandler.Mapper.AppendToMapping(nameof(Button.BorderColor), ApplyButton);
        ButtonHandler.Mapper.AppendToMapping(nameof(Button.BorderWidth), ApplyButton);
        ButtonHandler.Mapper.AppendToMapping(nameof(Button.CornerRadius), ApplyButton);
        CollectionViewHandler.Mapper.AppendToMapping(nameof(VisualElement.Background), ApplyCollection);
        ContentViewHandler.Mapper.AppendToMapping(nameof(VisualElement.WidthRequest), LockPaper);
        ContentViewHandler.Mapper.AppendToMapping(nameof(VisualElement.HeightRequest), LockPaper);
    }

    private static void ApplyButton(IButtonHandler handler, IButton view)
    {
        if (handler.PlatformView is not Microsoft.UI.Xaml.Controls.Button native
            || view is not Button button)
            return;

        if (button.BackgroundColor is { } fill)
            native.Background = ToBrush(fill);
        if (button.TextColor is { } text)
            native.Foreground = ToBrush(text);
        if (button.BorderColor is { } border)
            native.BorderBrush = ToBrush(border);

        // MAUI defaults BorderWidth/CornerRadius to -1 ("unset"). WinUI Measure throws
        // ArgumentException ("Value does not fall within the expected range") for negatives.
        if (button.BorderWidth >= 0 && double.IsFinite(button.BorderWidth))
            native.BorderThickness = new Microsoft.UI.Xaml.Thickness(button.BorderWidth);
        if (button.CornerRadius >= 0)
            native.CornerRadius = new Microsoft.UI.Xaml.CornerRadius(button.CornerRadius);
    }

    private static void ApplyCollection(CollectionViewHandler handler, CollectionView view)
    {
        if (handler.PlatformView is Microsoft.UI.Xaml.FrameworkElement root
            && !Equals(root.Tag, PaperItemsTag))
        {
            root.Tag = PaperItemsTag;
            root.Loaded += (_, _) => StyleListItems(root, view);
        }

        StyleListItems(handler.PlatformView as Microsoft.UI.Xaml.DependencyObject, view);
    }

    private static void StyleListItems(Microsoft.UI.Xaml.DependencyObject? node, CollectionView view)
    {
        if (node is null)
            return;
        if (node is Microsoft.UI.Xaml.Controls.ListViewBase list)
        {
            var fill = view.BackgroundColor is { } color
                ? ToBrush(color)
                : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
            var style = new Microsoft.UI.Xaml.Style(typeof(Microsoft.UI.Xaml.Controls.ListViewItem));
            style.Setters.Add(new Microsoft.UI.Xaml.Setter(
                Microsoft.UI.Xaml.Controls.Control.BackgroundProperty, fill));
            style.Setters.Add(new Microsoft.UI.Xaml.Setter(
                Microsoft.UI.Xaml.Controls.Control.PaddingProperty, new Microsoft.UI.Xaml.Thickness(0)));
            style.Setters.Add(new Microsoft.UI.Xaml.Setter(
                Microsoft.UI.Xaml.Controls.Control.HorizontalContentAlignmentProperty,
                Microsoft.UI.Xaml.HorizontalAlignment.Stretch));
            style.Setters.Add(new Microsoft.UI.Xaml.Setter(
                Microsoft.UI.Xaml.Controls.Control.VerticalContentAlignmentProperty,
                Microsoft.UI.Xaml.VerticalAlignment.Top));
            style.Setters.Add(new Microsoft.UI.Xaml.Setter(Microsoft.UI.Xaml.FrameworkElement.MinHeightProperty, 0d));
            style.Setters.Add(new Microsoft.UI.Xaml.Setter(Microsoft.UI.Xaml.FrameworkElement.MinWidthProperty, 0d));
            list.ItemContainerStyle = style;
        }

        if (node is not Microsoft.UI.Xaml.DependencyObject parent)
            return;
        var count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (var index = 0; index < count; index++)
            StyleListItems(Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, index), view);
    }

    private static void LockPaper(IContentViewHandler handler, IContentView view)
    {
        if (view is not PdfPageView page
            || handler.PlatformView is not Microsoft.UI.Xaml.FrameworkElement native)
            return;
        if (page.WidthRequest > 0)
        {
            native.Width = page.WidthRequest;
            native.MaxWidth = page.WidthRequest;
        }

        if (page.HeightRequest > 0)
        {
            native.Height = page.HeightRequest;
            native.MaxHeight = page.HeightRequest;
        }

        native.HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center;
        native.VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Top;
    }

    private static Microsoft.UI.Xaml.Media.SolidColorBrush ToBrush(Color color) =>
        new(Windows.UI.Color.FromArgb(
            (byte)System.Math.Clamp((int)System.Math.Round(color.Alpha * 255), 0, 255),
            (byte)System.Math.Clamp((int)System.Math.Round(color.Red * 255), 0, 255),
            (byte)System.Math.Clamp((int)System.Math.Round(color.Green * 255), 0, 255),
            (byte)System.Math.Clamp((int)System.Math.Round(color.Blue * 255), 0, 255)));
}
