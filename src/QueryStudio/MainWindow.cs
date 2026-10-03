using Avalonia.Controls;
using Avalonia.Layout;
using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using Novolis.Avalonia.GraphicalProfile;
using System.Data.Common;

namespace QueryStudio;

internal sealed class MainWindow : Window
{
    private readonly ComboBox _provider = new()
    {
        ItemsSource = new[] { "SQLite", "SQL Server" },
        SelectedIndex = 0,
        Width = 140,
    };
    private readonly TextBox _connectionString = new()
    {
        Text = "Data Source=:memory:",
        HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
        Width = 420,
    };
    private readonly TextBox _query = new()
    {
        AcceptsReturn = true,
        Height = 180,
        Text = "select 1 as value;",
    };
    private readonly TextBlock _result = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };

    public MainWindow()
    {
        GraphicalProfileBinding.Bind(this, BackgroundProperty, GraphicalProfile.BackgroundResourceKey);
        Title = "Query Studio";
        Width = 960;
        Height = 680;
        var execute = new Button { Content = "Run query", HorizontalAlignment = HorizontalAlignment.Left };
        execute.Click += (_, _) => Execute();
        Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(24),
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = "Query Studio", FontSize = 24 },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock { Text = "Provider:", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center },
                        _provider,
                        new TextBlock { Text = "Connection:", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center },
                        _connectionString,
                    },
                },
                _query,
                execute,
                _result,
            },
        };
    }

    private void Execute()
    {
        try
        {
            var connectionString = _connectionString.Text ?? string.Empty;
            using DbConnection connection = string.Equals(
                _provider.SelectedItem?.ToString(),
                "SQL Server",
                StringComparison.Ordinal)
                ? new SqlConnection(connectionString)
                : new SqliteConnection(connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = _query.Text ?? string.Empty;
            using var reader = command.ExecuteReader();
            var lines = new List<string>();
            while (reader.Read())
            {
                lines.Add(string.Join(" | ", Enumerable.Range(0, reader.FieldCount).Select(reader.GetValue)));
            }

            _result.Text = lines.Count == 0 ? "Query completed with no rows." : string.Join(Environment.NewLine, lines);
        }
        catch (Exception exception)
        {
            _result.Text = exception.Message;
        }
    }
}
