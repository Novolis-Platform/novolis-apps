using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Avalonia.GraphicalProfile;
using Novolis.Avalonia.Agent;
using Novolis.Hours.Client;

namespace Novolis.Hours.Client.Avalonia;

/// <summary>Native sign-in surface for a secured self-hosted Hours service.</summary>
public sealed class MainWindow : Window
{
    private HoursApiClient? apiClient;
    private AgentHost? agentHost;

    /// <summary>Initializes the native client shell.</summary>
    public MainWindow()
    {
        Title = "Novolis Hours";
        Width = 720;
        Height = 420;
        MinWidth = 520;
        MinHeight = 320;
        GraphicalProfileBinding.Bind(this, BackgroundProperty, GraphicalProfile.BackgroundResourceKey);

        var endpoint = new TextBox
        {
            Text = Environment.GetEnvironmentVariable("NOVOLIS_HOURS_SERVICE_URL") ?? string.Empty,
            PlaceholderText = "Hours service URL",
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AgentProperties.SetId(endpoint, "hours.service-url");
        var login = new TextBox
        {
            Text = "admin",
            PlaceholderText = "Login",
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AgentProperties.SetId(login, "hours.login");
        var password = new TextBox
        {
            PasswordChar = '●',
            PlaceholderText = "Password",
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AgentProperties.SetId(password, "hours.password");
        var connect = new Button
        {
            Content = "Sign in and load summary",
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        AgentProperties.SetId(connect, "hours.sign-in");
        var status = new TextBlock
        {
            Text = "Enter a secure Hours service URL and sign in.",
            TextWrapping = TextWrapping.Wrap,
        };
        AgentProperties.SetId(status, "hours.status");
        connect.Click += async (_, _) =>
        {
            connect.IsEnabled = false;
            status.Text = "Signing in…";
            try
            {
                var serviceUri = new Uri(endpoint.Text ?? string.Empty, UriKind.Absolute);
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
                connect.IsEnabled = true;
            }
        };
        var content = new StackPanel
        {
            Margin = new Thickness(32),
            Spacing = 14,
            Children =
            {
                new TextBlock
                {
                    Text = "Novolis Hours",
                    FontSize = 28,
                    FontWeight = FontWeight.SemiBold,
                },
                new TextBlock
                {
                    Text = "Sign in to the self-hosted Hours service to view your recorded presence and flex-time summary.",
                    TextWrapping = TextWrapping.Wrap,
                },
                new TextBlock { Text = "Service URL" },
                endpoint,
                new TextBlock { Text = "Login" },
                login,
                new TextBlock { Text = "Password" },
                password,
                connect,
                status,
            },
        };
        Content = content;
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
}
