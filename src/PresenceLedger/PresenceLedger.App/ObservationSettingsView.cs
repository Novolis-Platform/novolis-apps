using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Avalonia.GraphicalProfile;
using PresenceLedger.Core;

namespace PresenceLedger.App;

/// <summary>Polling, freshness, publish, and time-export settings.</summary>
internal static class ObservationSettingsView
{
    public static Control Build(
        ObservationSettings settings,
        Func<ObservationSettings, Task> save)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(save);

        var poll = NumberBox(settings.PollIntervalSeconds, 10, 600);
        var fixAge = NumberBox(settings.FreshFixAgeMinutes, 1, 60);
        var networkAge = NumberBox(settings.FreshNetworkAgeMinutes, 1, 60);
        var publishMinutes = NumberBox(settings.AutoPublishMinimumIntervalMinutes, 1, 24 * 60);
        var autoPublish = new CheckBox
        {
            Content = "Automatically publish ledger copies to Downloads",
            IsChecked = settings.AutoPublishDownloads,
            Foreground = GraphicalProfile.TextBrush,
        };
        var clockifyProject = new TextBox
        {
            Text = settings.ClockifyProjectName ?? string.Empty,
            PlaceholderText = "Optional Clockify project name",
            MinHeight = 44,
        };
        var clockifyBillable = new CheckBox
        {
            Content = "Mark Clockify exports as billable",
            IsChecked = settings.ClockifyBillable,
            Foreground = GraphicalProfile.TextBrush,
        };

        var stack = new StackPanel
        {
            Margin = new Thickness(16, 12, 16, 28),
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        stack.Children.Add(Eyebrow("OBSERVATION"));
        stack.Children.Add(Title("Settings"));
        stack.Children.Add(Hint(
            "Tune how often the phone samples evidence, how long a fix stays fresh, and how aggressively Downloads is updated."));

        stack.Children.Add(Field("Poll interval (seconds)", poll));
        stack.Children.Add(Field("GPS fix freshness (minutes)", fixAge));
        stack.Children.Add(Field("Wi-Fi freshness (minutes)", networkAge));
        stack.Children.Add(autoPublish);
        stack.Children.Add(Field("Minimum publish interval (minutes)", publishMinutes));
        stack.Children.Add(Hint(
            "Leave automatic Downloads publishing off, or raise the interval, if MediaStore starts leaving pending files."));

        stack.Children.Add(Eyebrow("TIME EXPORT"));
        stack.Children.Add(Hint(
            "Clockify and Hours exports come from confirmed stays on the Report page. These options only set Clockify defaults."));
        stack.Children.Add(Field("Clockify project", clockifyProject));
        stack.Children.Add(clockifyBillable);

        var saveButton = new Button
        {
            Content = "Save settings",
            Classes = { "primary-button" },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 48,
            Background = GraphicalProfile.AccentFillBrush,
            Foreground = GraphicalProfile.TextBrush,
            Margin = new Thickness(0, 8, 0, 0),
        };
        saveButton.Click += async (_, _) =>
        {
            var next = new ObservationSettings(
                pollIntervalSeconds: (int)(poll.Value ?? 30),
                freshFixAgeMinutes: (int)(fixAge.Value ?? 2),
                freshNetworkAgeMinutes: (int)(networkAge.Value ?? 2),
                autoPublishDownloads: autoPublish.IsChecked == true,
                autoPublishMinimumIntervalMinutes: (int)(publishMinutes.Value ?? 15),
                clockifyProjectName: clockifyProject.Text,
                clockifyBillable: clockifyBillable.IsChecked == true);
            await save(next);
        };
        stack.Children.Add(saveButton);
        return stack;
    }

    static NumericUpDown NumberBox(int value, int min, int max) =>
        new()
        {
            Value = value,
            Minimum = min,
            Maximum = max,
            Increment = 1,
            FormatString = "0",
            MinHeight = 44,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

    static Control Field(string label, Control editor)
    {
        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = GraphicalProfile.MutedBrush,
            FontSize = 12,
        });
        stack.Children.Add(editor);
        return stack;
    }

    static TextBlock Eyebrow(string text) =>
        new()
        {
            Text = text,
            Classes = { "eyebrow" },
            Foreground = GraphicalProfile.AccentBrush,
            Margin = new Thickness(0, 8, 0, 0),
        };

    static TextBlock Title(string text) =>
        new()
        {
            Text = text,
            FontSize = 26,
            FontWeight = FontWeight.SemiBold,
            Foreground = GraphicalProfile.TextBrush,
        };

    static TextBlock Hint(string text) =>
        new()
        {
            Text = text,
            Foreground = GraphicalProfile.MutedBrush,
            TextWrapping = TextWrapping.Wrap,
        };
}
