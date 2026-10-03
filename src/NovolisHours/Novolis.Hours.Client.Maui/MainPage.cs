using Microsoft.Maui.Controls.Shapes;
using Novolis.Maui.GraphicalProfile;
using Novolis.Hours.Client;

namespace Novolis.Hours.Client.Maui;

/// <summary>Profile-compliant native sign-in surface for the Hours service.</summary>
public sealed class MainPage : ContentPage
{
    private HoursApiClient? apiClient;

    /// <summary>Initializes the native Hours client page.</summary>
    public MainPage()
    {
        Title = "Novolis Hours";
        BackgroundColor = GraphicalProfile.Background;
        var serviceUrl = new Entry
        {
            Text = "https://localhost:5001",
            Placeholder = "Hours service URL",
            Keyboard = Keyboard.Url,
        };
        var login = new Entry
        {
            Text = "admin",
            Placeholder = "Login",
        };
        var password = new Entry
        {
            Placeholder = "Password",
            IsPassword = true,
        };
        var signIn = new Button { Text = "Sign in and load summary" };
        var status = new Label
        {
            Text = "Enter a secure Hours service URL and sign in.",
            TextColor = GraphicalProfile.Muted,
        };
        signIn.Clicked += async (_, _) =>
        {
            signIn.IsEnabled = false;
            status.Text = "Signing in…";
            try
            {
                var serviceUri = new Uri(serviceUrl.Text ?? string.Empty, UriKind.Absolute);
                apiClient?.Dispose();
                apiClient = HoursApiClient.Connect(serviceUri);
                var user = await apiClient.SignInAsync(login.Text ?? string.Empty, password.Text ?? string.Empty);
                var summary = await apiClient.GetEmployeeSummaryAsync(user.EmployeeId);
                status.Text =
                    $"{user.DisplayName} ({user.Role}) · flex {summary.FlexSaldo:c} · " +
                    $"{summary.PresenceRecordCount} presence record(s) · {summary.AnomalyCount} anomaly/anomalies.";
            }
            catch (Exception exception)
            {
                status.Text = $"Could not sign in: {exception.Message}";
            }
            finally
            {
                signIn.IsEnabled = true;
            }
        };
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(28),
                Spacing = 14,
                Children =
                {
                    new Label
                    {
                        Text = "Novolis Hours",
                        FontSize = 30,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = GraphicalProfile.Text,
                    },
                    new Label
                    {
                        Text = "Sign in to a secured Novolis Hours service and load your worktime summary.",
                        FontSize = 16,
                        TextColor = GraphicalProfile.Muted,
                    },
                    new Border
                    {
                        Stroke = new SolidColorBrush(GraphicalProfile.Border),
                        Background = new SolidColorBrush(GraphicalProfile.Surface),
                        StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(14) },
                        Padding = 18,
                        Content = new Label
                        {
                            Text = "This client uses the same protected HTTP API as the browser app. It never calculates pay or leave.",
                            TextColor = GraphicalProfile.Text,
                        },
                    },
                    new Label { Text = "Service URL", TextColor = GraphicalProfile.Text },
                    serviceUrl,
                    new Label { Text = "Login", TextColor = GraphicalProfile.Text },
                    login,
                    new Label { Text = "Password", TextColor = GraphicalProfile.Text },
                    password,
                    signIn,
                    status,
                },
            },
        };
    }

    /// <inheritdoc />
    protected override void OnDisappearing()
    {
        apiClient?.Dispose();
        apiClient = null;
        base.OnDisappearing();
    }
}
