using Avalonia.Controls;
using Avalonia.Layout;
using Novolis.Avalonia.GraphicalProfile;
using Novolis.Http.Client;
using Novolis.Http.Documents;
using Novolis.Http.Variables;

namespace HttpStudio;

internal sealed class MainWindow : Window
{
    private readonly ComboBox _method = new() { ItemsSource = new[] { "GET", "POST", "PUT", "PATCH", "DELETE" }, SelectedIndex = 0 };
    private readonly TextBox _uri = new() { Text = "https://example.test/" };
    private readonly TextBox _body = new() { AcceptsReturn = true, Height = 140, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly TextBlock _result = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };

    public MainWindow()
    {
        GraphicalProfileBinding.Bind(this, BackgroundProperty, GraphicalProfile.BackgroundResourceKey);
        Title = "HTTP Studio";
        Width = 960;
        Height = 680;

        var send = new Button { Content = "Send", HorizontalAlignment = HorizontalAlignment.Left };
        send.Click += async (_, _) => await SendAsync();

        Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(24),
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = "HTTP Studio", FontSize = 24 },
                new TextBlock { Text = "Request document" },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children = { _method, _uri, send },
                },
                new TextBlock { Text = "Body" },
                _body,
                new TextBlock { Text = "Execution result" },
                _result,
            },
        };
    }

    private async Task SendAsync()
    {
        try
        {
            var document = new HttpRequestDocument
            {
                Method = _method.SelectedItem?.ToString() ?? "GET",
                Uri = _uri.Text ?? string.Empty,
                Body = string.IsNullOrWhiteSpace(_body.Text) ? null : _body.Text,
                ContentType = "application/json",
            };
            var resolved = await new TemplateHttpVariableResolver().ResolveAsync(
                document,
                new HttpEnvironment());
            using var request = resolved.CreateRequest();
            using var client = new HttpClient();
            var response = await new RestClient(client, [], []).SendAsync(request, CancellationToken.None);
            _result.Text = $"{(int)response.StatusCode} {response.ReasonPhrase}\n{await response.Content.ReadAsStringAsync()}";
        }
        catch (Exception exception)
        {
            _result.Text = exception.Message;
        }
    }
}
