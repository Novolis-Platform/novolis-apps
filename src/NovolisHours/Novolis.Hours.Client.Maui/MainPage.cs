using Microsoft.Maui.Controls.Shapes;
using Novolis.Hours.Client;
using Novolis.Hours.Client.Presentation;
using Novolis.Hours.Contracts;
using Novolis.Maui.Agent;
using Novolis.Maui.GraphicalProfile;

namespace Novolis.Hours.Client.Maui;

/// <summary>Hours MAUI shell: door, compact week, and a scrollable day strip.</summary>
public sealed class MainPage : ContentPage
{
    private readonly HoursSessionModel session = HoursSessionPersistence.Load();
    private readonly Entry serviceUrl = new();
    private readonly Entry login = new();
    private readonly Entry password = new();
    private readonly Label status = new();
    private readonly Label person = new();
    private readonly Label organisation = new();
    private readonly Label flex = new();
    private readonly VerticalStackLayout body = new();
    private HoursApiClient? apiClient;
    private HoursClientUser? currentUser;
    private DayStudioModel? studio;
    private bool paintMode;
    private AgentHost? agentHost;

    /// <summary>Initializes the native Hours product page.</summary>
    public MainPage()
    {
        Title = "Novolis Hours";
        BackgroundColor = GraphicalProfile.Background;
        serviceUrl.Text = session.ServiceUrl;
        serviceUrl.Placeholder = "Hours service URL";
        serviceUrl.Keyboard = Keyboard.Url;
        serviceUrl.AutomationId = "hours.service-url";
        login.Text = session.EmployeeId ?? "ada";
        login.Placeholder = "Login";
        login.AutomationId = "hours.login";
        password.Placeholder = "Password";
        password.IsPassword = true;
        password.AutomationId = "hours.password";
        status.Text = "Sign in to open this week.";
        status.TextColor = GraphicalProfile.Muted;
        status.AutomationId = "hours.status";
        person.Text = "Hours";
        person.TextColor = GraphicalProfile.Text;
        organisation.TextColor = GraphicalProfile.Muted;
        flex.TextColor = GraphicalProfile.Muted;
        ShowDoor();
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 14,
                Children =
                {
                    Chrome(),
                    body,
                },
            },
        };
        agentHost = AgentHost.TryAttachFromEnvironment(this);
    }

    /// <inheritdoc />
    protected override void OnDisappearing()
    {
        apiClient?.Dispose();
        apiClient = null;
        var host = agentHost;
        agentHost = null;
        _ = host?.DisposeAsync();
        base.OnDisappearing();
    }

    private View Chrome()
    {
        var week = new Button { Text = "Week", AutomationId = "hours.week" };
        week.Clicked += async (_, _) => await ShowWeekAsync();
        var today = new Button { Text = "Today" };
        today.Clicked += async (_, _) => await ShowDayAsync(DateOnly.FromDateTime(DateTime.Today));
        var settings = new Button { Text = "Settings" };
        settings.Clicked += (_, _) => ShowSettings();
        return new Border
        {
            Stroke = new SolidColorBrush(GraphicalProfile.Border),
            Background = new SolidColorBrush(GraphicalProfile.Surface),
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(8) },
            Padding = 12,
            Content = new HorizontalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    new Label
                    {
                        Text = "Hours",
                        FontSize = 20,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = GraphicalProfile.Text,
                        VerticalOptions = LayoutOptions.Center,
                    },
                    person,
                    organisation,
                    flex,
                    week,
                    today,
                    settings,
                },
            },
        };
    }

    private void ShowDoor()
    {
        var signIn = new Button
        {
            Text = "Enter this week",
            AutomationId = "hours.sign-in",
            BackgroundColor = GraphicalProfile.Action,
            TextColor = GraphicalProfile.OnAction,
        };
        signIn.Clicked += async (_, _) => await SignInAsync();
        ReplaceBody(
            Eyebrow("Sign in"),
            Heading("Hours"),
            LabelOf("Login"),
            login,
            LabelOf("Password"),
            password,
            signIn,
            status);
    }

    private void ShowSettings()
    {
        var save = new Button { Text = "Save", BackgroundColor = GraphicalProfile.Action, TextColor = GraphicalProfile.OnAction };
        save.Clicked += (_, _) =>
        {
            session.ServiceUrl = serviceUrl.Text ?? session.ServiceUrl;
            HoursSessionPersistence.Save(session);
            status.Text = "Service URL saved.";
        };
        ReplaceBody(Eyebrow("Settings"), Heading("Host"), LabelOf("Hours service URL"), serviceUrl, save, status);
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
        var week = new WeekStudioModel(days, DateOnly.FromDateTime(DateTime.Today));
        if (week.Tiles.Length > 0)
        {
            organisation.Text = $"{week.Tiles[0].Day.OrganisationId} · {week.Tiles[0].Day.Configuration.TimeZoneId}";
        }

        var summary = await apiClient.GetEmployeeSummaryAsync(currentUser.EmployeeId);
        flex.Text = $"Flex {HoursClock.Format(summary.FlexSaldo, signed: true)}";
        var tiles = new HorizontalStackLayout { Spacing = 8 };
        foreach (var tile in week.Tiles)
        {
            var date = tile.Date;
            var button = new Button
            {
                Text = $"{tile.Weekday}{Environment.NewLine}{tile.Expected}{Environment.NewLine}{tile.Chip}",
                MinimumHeightRequest = 42,
                MinimumWidthRequest = 72,
            };
            button.Clicked += async (_, _) => await ShowDayAsync(date);
            tiles.Children.Add(button);
        }

        ReplaceBody(
            Eyebrow("Week"),
            Heading($"{HoursClock.FormatDate(week.WeekStart)} – {HoursClock.FormatDate(week.WeekEnd)}"),
            new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = tiles },
            status);
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
        var expected = new Label { TextColor = GraphicalProfile.Text };
        var actual = new Label { TextColor = GraphicalProfile.Text };
        var flexDay = new Label { TextColor = GraphicalProfile.Text };
        var rail = new VerticalStackLayout { Spacing = 6 };
        var strip = new HoursDayStripView
        {
            Studio = studio,
            AutomationId = "hours.day",
        };
        void Refresh()
        {
            if (studio is null)
            {
                return;
            }

            expected.Text = studio.ExpectedLabel;
            actual.Text = HoursClock.Format(studio.ActualWork);
            flexDay.Text = HoursClock.Format(studio.Flex, signed: true);
            rail.Children.Clear();
            foreach (var row in studio.LayerRail())
            {
                rail.Children.Add(new Label
                {
                    Text = string.IsNullOrWhiteSpace(row.Said)
                        ? $"{row.Order} · {row.Kind}"
                        : $"{row.Order} · {row.Kind} · {row.Said}",
                    TextColor = row.Speaking ? GraphicalProfile.Text : GraphicalProfile.Muted,
                });
            }

            strip.Invalidate();
        }

        strip.StudioChanged += (_, _) => Refresh();
        Refresh();
        var commit = new Button
        {
            Text = "Worked as scheduled",
            AutomationId = "hours.worked-as-scheduled",
            BackgroundColor = GraphicalProfile.Action,
            TextColor = GraphicalProfile.OnAction,
        };
        commit.Clicked += async (_, _) =>
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
                "Worked as scheduled."));
            studio.ApplyScheduledRoutine();
            status.Text = "Scheduled work recorded.";
            Refresh();
        };
        var paint = new Button { Text = "Paint" };
        paint.Clicked += (_, _) =>
        {
            paintMode = !paintMode;
            strip.PaintMode = paintMode;
            paint.Text = paintMode ? "Adjust times" : "Paint";
        };
        ReplaceBody(
            Eyebrow($"{day.OrganisationId} · {day.Configuration.TimeZoneId}"),
            Heading($"{HoursClock.FormatDate(date)} · {day.OrganisationId}"),
            new HorizontalStackLayout { Spacing = 8, Children = { commit, paint } },
            Metric("Expected", expected),
            Metric("Actual", actual),
            Metric("Flex", flexDay),
            new ScrollView
            {
                Orientation = ScrollOrientation.Horizontal,
                Content = strip,
            },
            new Label
            {
                Text = "06    08    10    12    14    16    18    20",
                FontFamily = "Consolas",
                TextColor = GraphicalProfile.Muted,
            },
            rail,
            status);
    }

    private void ReplaceBody(params IView[] children)
    {
        body.Children.Clear();
        foreach (var child in children)
        {
            body.Children.Add(child);
        }
    }

    private static Label Eyebrow(string text) =>
        new()
        {
            Text = text.ToUpperInvariant(),
            FontSize = 11,
            TextColor = GraphicalProfile.Muted,
        };

    private static Label Heading(string text) =>
        new()
        {
            Text = text,
            FontSize = 26,
            FontAttributes = FontAttributes.Bold,
            TextColor = GraphicalProfile.Text,
        };

    private static Label LabelOf(string text) =>
        new() { Text = text, TextColor = GraphicalProfile.Text };

    private static View Metric(string label, Label value) =>
        new VerticalStackLayout
        {
            Children =
            {
                new Label { Text = label, TextColor = GraphicalProfile.Muted },
                value,
            },
        };
}
