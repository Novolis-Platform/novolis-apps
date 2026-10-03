using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Avalonia.GraphicalProfile;
using Novolis.Language.RoboSharp;

namespace RoboSharpStudio;

internal sealed class MainWindow : Window
{
    private readonly TextBox _source = new()
    {
        AcceptsReturn = true,
        FontFamily = "Consolas",
        Text = "move 2\nturn right\nprint \"hello\"",
        TextWrapping = TextWrapping.NoWrap,
    };
    private readonly TextBlock _pipeline = new() { TextWrapping = TextWrapping.Wrap };
    private RoboSharpExecutionSession? _session;

    public MainWindow()
    {
        GraphicalProfileBinding.Bind(this, BackgroundProperty, GraphicalProfile.BackgroundResourceKey);
        Title = "RoboSharp Studio";
        Width = 1080;
        Height = 720;

        var compile = new Button { Content = "Compile" };
        compile.Click += (_, _) => Compile();
        var step = new Button { Content = "Step" };
        step.Click += (_, _) =>
        {
            if (_session is not null)
            {
                var result = _session.Step();
                _pipeline.Text = $"{result.Kind}: {result.Description}\n{FormatSnapshot(_session.Snapshot)}";
            }
        };

        Content = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Margin = new Thickness(24),
            RowSpacing = 12,
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock { Text = "RoboSharp Studio", FontSize = 24, VerticalAlignment = VerticalAlignment.Center },
                        compile,
                        step,
                    },
                },
                _source,
                _pipeline,
            },
        };
        Grid.SetRow(_source, 1);
        Grid.SetRow(_pipeline, 2);
        Compile();
    }

    private void Compile()
    {
        var compilation = new RoboSharpCompiler().Compile(_source.Text ?? string.Empty);
        if (!compilation.Succeeded)
        {
            _session = null;
            _pipeline.Text = string.Join(Environment.NewLine, compilation.Diagnostics.Select(item => $"{item.Code}: {item.Message}"));
            return;
        }

        _session = new RoboSharpExecutionSession(compilation.Program!);
        _pipeline.Text = string.Join(Environment.NewLine, compilation.TeachingInstructions.Select(item => item.Display));
    }

    private static string FormatSnapshot(Novolis.Language.Execution.ExecutionSnapshot snapshot) =>
        $"x={snapshot.Variables["x"]}, y={snapshot.Variables["y"]}, heading={snapshot.Variables["heading"]}, output={string.Join(", ", snapshot.Output)}";
}
