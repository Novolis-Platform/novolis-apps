using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Avalonia.Agent;
using Novolis.Avalonia.GraphicalProfile;
using Novolis.Hours.Client;
using Novolis.Hours.Client.Presentation;
using Novolis.Hours.Contracts;

namespace Novolis.Hours.Client.Avalonia;

/// <summary>Hours desktop shell: door, week, and day studio.</summary>
public sealed class MainWindow : Window
{
    private readonly HoursSessionModel session = HoursSessionPersistence.Load();
    private readonly TextBox serviceUrl = new();
    private readonly TextBox login = new();
    private readonly TextBox password = new();
    private readonly TextBlock status = new();
    private readonly ContentControl body = new();
    private readonly TextBlock person = new();
    private readonly TextBlock organisation = new();
    private readonly TextBlock flex = new();
    private HoursApiClient? apiClient;
    private HoursClientUser? currentUser;
    private WeekStudioModel? week;
    private DayStudioModel? studio;
    private bool paintMode;
    private AgentHost? agentHost;

    /// <summary>Initializes the native Hours product window.</summary>
    public MainWindow()
    {
        base.Title = "Novolis Hours";
        Width = 1100;
        Height = 760;
        MinWidth = 720;
        MinHeight = 480;
        GraphicalProfileBinding.Bind(this, BackgroundProperty, GraphicalProfile.BackgroundResourceKey);
        serviceUrl.Text = session.ServiceUrl;
        serviceUrl.PlaceholderText = "Hours service URL";
        AgentProperties.SetId(serviceUrl, "hours.service-url");
        login.Text = session.EmployeeId ?? "ada";
        login.PlaceholderText = "Login";
        AgentProperties.SetId(login, "hours.login");
        password.PasswordChar = '●';
        password.PlaceholderText = "Password";
        AgentProperties.SetId(password, "hours.password");
        status.Text = "Sign in to open this week.";
        status.TextWrapping = TextWrapping.Wrap;
        AgentProperties.SetId(status, "hours.status");
        person.Text = "Hours";
        organisation.Foreground = GraphicalProfile.MutedBrush;
        flex.Foreground = GraphicalProfile.MutedBrush;
        ShowDoor();
        var chrome = Chrome();
        DockPanel.SetDock(chrome, Dock.Top);
        Content = new DockPanel
        {
            Margin = new Thickness(24),
            LastChildFill = true,
            Children =
            {
                chrome,
                body,
            },
        };

        agentHost = AgentHost.TryAttachFromEnvironment(this);
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        apiClient?.Dispose();
        var host = agentHost;
        agentHost = null;
        _ = host?.DisposeAsync();
        base.OnClosed(e);
    }

    private Control Chrome()
    {
        var weekButton = new Button { Content = "Week" };
        AgentProperties.SetId(weekButton, "hours.week");
        weekButton.Click += async (_, _) => await ShowWeekAsync();
        var todayButton = new Button { Content = "Today" };
        todayButton.Click += async (_, _) =>
            await ShowDayAsync(DateOnly.FromDateTime(DateTime.Today));
        var settingsButton = new Button { Content = "Settings" };
        settingsButton.Click += (_, _) => ShowSettings();
        return new Border
        {
            Background = GraphicalProfile.SurfaceBrush,
            BorderBrush = GraphicalProfile.BorderBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 10),
            Margin = new Thickness(0, 0, 0, 16),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Hours",
                        FontSize = 20,
                        FontWeight = FontWeight.SemiBold,
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                    person,
                    organisation,
                    flex,
                    weekButton,
                    todayButton,
                    settingsButton,
                },
            },
        };
    }

    private void ShowDoor()
    {
        var signIn = new Button
        {
            Content = "Enter this week",
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = GraphicalProfile.ActionBrush,
            Foreground = GraphicalProfile.OnActionBrush,
        };
        AgentProperties.SetId(signIn, "hours.sign-in");
        signIn.Click += async (_, _) => await SignInAsync();
        body.Content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                Eyebrow("Sign in"),
                Heading("Hours"),
                Labeled("Login", login),
                Labeled("Password", password),
                signIn,
                status,
            },
        };
    }

    private void ShowSettings()
    {
        var save = new Button { Content = "Save", HorizontalAlignment = HorizontalAlignment.Left };
        save.Click += (_, _) =>
        {
            session.ServiceUrl = serviceUrl.Text ?? session.ServiceUrl;
            HoursSessionPersistence.Save(session);
            status.Text = "Service URL saved.";
        };
        body.Content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                Eyebrow("Settings"),
                Heading("Host"),
                Labeled("Hours service URL", serviceUrl),
                save,
                status,
            },
        };
    }

    private async Task SignInAsync()
    {
        status.Text = "Signing in…";
        try
        {
            session.ServiceUrl = serviceUrl.Text ?? session.ServiceUrl;
            apiClient?.Dispose();
            apiClient = HoursApiClient.Connect(new Uri(session.ServiceUrl, UriKind.Absolute));
            currentUser = await apiClient.SignInAsync(login.Text ?? string.Empty, password.Text ?? string.Empty);
            session.EmployeeId = currentUser.EmployeeId;
            session.DisplayName = currentUser.DisplayName;
            HoursSessionPersistence.Save(session);
            person.Text = currentUser.DisplayName;
            await ShowWeekAsync();
        }
        catch (Exception exception)
        {
            status.Text = exception.Message;
        }
    }

    private async Task ShowWeekAsync()
    {
        if (apiClient is null || currentUser is null)
        {
            ShowDoor();
            return;
        }

        var start = WeekStudioModel.MondayOnOrBefore(DateOnly.FromDateTime(DateTime.Today));
        var days = await apiClient.GetWorkDaysAsync(currentUser.EmployeeId, start, start.AddDays(6));
        week = new WeekStudioModel(days, DateOnly.FromDateTime(DateTime.Today));
        if (week.Tiles.Length > 0)
        {
            var first = week.Tiles[0].Day;
            var workplace = string.IsNullOrWhiteSpace(first.OrganisationName)
                ? first.OrganisationId
                : first.OrganisationName;
            organisation.Text = $"{workplace} · {first.Configuration.TimeZoneId}";
            session.OrganisationId = first.OrganisationId;
            session.TimeZoneId = first.Configuration.TimeZoneId;
            if (first.AllowsFlex)
            {
                var summary = await apiClient.GetEmployeeSummaryAsync(currentUser.EmployeeId);
                flex.Text = $"Flex {HoursClock.Format(summary.FlexSaldo, signed: true)}";
                flex.IsVisible = true;
            }
            else
            {
                flex.Text = first.AttendanceConfirmationOnly ? "Shop hours" : string.Empty;
                flex.IsVisible = !string.IsNullOrWhiteSpace(flex.Text);
            }
        }
        var tiles = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var tile in week.Tiles)
        {
            var button = new Button
            {
                Content = new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = tile.Weekday, Foreground = GraphicalProfile.MutedBrush },
                        new TextBlock { Text = tile.Expected, FontWeight = FontWeight.SemiBold },
                        new TextBlock { Text = tile.Chip, Foreground = GraphicalProfile.MutedBrush },
                    },
                },
                MinHeight = 72,
                Padding = new Thickness(10),
            };
            var date = tile.Date;
            button.Click += async (_, _) => await ShowDayAsync(date);
            tiles.Children.Add(button);
        }

        var weekPanel = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                Eyebrow("Week"),
                Heading($"{HoursClock.FormatDate(week.WeekStart)} – {HoursClock.FormatDate(week.WeekEnd)}"),
                tiles,
                status,
            },
        };
        AgentProperties.SetId(weekPanel, "hours.week");
        body.Content = weekPanel;
        status.Text = $"{currentUser.DisplayName} · this week.";
    }

    private async Task ShowDayAsync(DateOnly date)
    {
        if (apiClient is null || currentUser is null)
        {
            ShowDoor();
            return;
        }

        var day = await apiClient.GetWorkDayAsync(currentUser.EmployeeId, date);
        studio = new DayStudioModel(day);
        organisation.Text = $"{day.OrganisationId} · {day.Configuration.TimeZoneId}";
        var strip = new HoursDayStripControl
        {
            Studio = studio,
            Height = 108,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AgentProperties.SetId(strip, "hours.day");
        var expected = new TextBlock();
        var actual = new TextBlock();
        var flexDay = new TextBlock();
        void Refresh()
        {
            expected.Text = studio.ExpectedLabel;
            actual.Text = HoursClock.Format(studio.ActualWork);
            flexDay.Text = studio.AllowsFlex
                ? HoursClock.Format(studio.Flex, signed: true)
                : studio.BalanceLabel;
            strip.InvalidateVisual();
        }

        Refresh();
        strip.PointerReleased += (_, _) => Refresh();
        strip.PointerMoved += (_, _) =>
        {
            if (studio.IsDragging)
            {
                Refresh();
            }
        };

        var commit = new Button
        {
            Content = studio.CommitLabel,
            Background = GraphicalProfile.ActionBrush,
            Foreground = GraphicalProfile.OnActionBrush,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        AgentProperties.SetId(commit, "hours.worked-as-scheduled");
        commit.Click += async (_, _) =>
        {
            if (apiClient is null || studio is null || currentUser is null)
            {
                return;
            }

            await apiClient.RecordWorkRegistrationAsync(new RecordWorkRegistrationRequest(
                currentUser.EmployeeId,
                studio.Day.NominalDate,
                WorkRegistrationIntent.WorkedAsScheduled,
                [],
                null,
                studio.CommitNote));
            studio.ApplyScheduledRoutine();
            status.Text = "Scheduled work recorded.";
            Refresh();
        };

        var paint = new Button { Content = "Paint", HorizontalAlignment = HorizontalAlignment.Left };
        paint.Click += (_, _) =>
        {
            paintMode = !paintMode;
            strip.PaintMode = paintMode;
            paint.Content = paintMode ? "Adjust times" : "Paint";
        };

        var rail = new StackPanel { Spacing = 6 };
        void RenderRail()
        {
            rail.Children.Clear();
            foreach (var row in studio.LayerRail())
            {
                rail.Children.Add(new Border
                {
                    BorderBrush = row.Speaking ? GraphicalProfile.AccentFillBrush : GraphicalProfile.BorderBrush,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(8, 6),
                    Child = new TextBlock
                    {
                        Text = string.IsNullOrWhiteSpace(row.Said)
                            ? $"{row.Order} · {row.Kind}"
                            : $"{row.Order} · {row.Kind} · {row.Said}",
                    },
                });
            }
        }

        RenderRail();
        strip.PointerReleased += (_, _) => RenderRail();

        var dayPanel = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                Eyebrow($"{studio.WorkplaceName} · {day.Configuration.TimeZoneId}"),
                Heading($"{HoursClock.FormatDate(date)} · {studio.WorkplaceName}"),
                studio.AllowsPaint ? CommitRow(commit, paint) : CommitRow(commit),
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 24,
                    Children =
                    {
                        Metric("Expected", expected),
                        Metric("Actual", actual),
                        Metric(studio.AllowsFlex ? "Flex" : "Attendance", flexDay),
                    },
                },
                strip,
                new TextBlock
                {
                    Text = "06    08    10    12    14    16    18    20",
                    FontFamily = GraphicalProfile.MonoFont,
                    Foreground = GraphicalProfile.MutedBrush,
                },
                rail,
                status,
            },
        };
        AgentProperties.SetId(dayPanel, "hours.day");
        body.Content = dayPanel;
    }

    private static TextBlock Eyebrow(string text) =>
        new()
        {
            Text = text.ToUpperInvariant(),
            FontSize = 11,
            Foreground = GraphicalProfile.MutedBrush,
        };

    private static TextBlock Heading(string text) =>
        new()
        {
            Text = text,
            FontSize = 28,
            FontWeight = FontWeight.SemiBold,
        };

    private static Control Labeled(string label, Control field) =>
        new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock { Text = label },
                field,
            },
        };

    private static Control CommitRow(Button commit, Button? paint = null)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Children = { commit },
        };
        if (paint is not null)
        {
            row.Children.Add(paint);
        }

        AgentProperties.SetId(row, "hours.commit");
        return row;
    }

    private static Control Metric(string label, TextBlock value) =>
        new StackPanel
        {
            Children =
            {
                new TextBlock { Text = label, Foreground = GraphicalProfile.MutedBrush },
                value,
            },
        };
}
