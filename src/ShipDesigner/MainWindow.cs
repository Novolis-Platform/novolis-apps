using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Novolis.Avalonia.Cad.Commands;
using Novolis.Avalonia.Cad.Core;
using Novolis.Avalonia.Cad.Services;
using Novolis.Avalonia.Cad.Session;
using Novolis.Avalonia.Cad.Ship;
using Novolis.Avalonia.Cad.Ui;
using Novolis.Avalonia.Ship;
using Novolis.Avalonia.Ship.Ui;
using Novolis.Ship.Primitives;

namespace ShipDesigner;

internal sealed class MainWindow : Window
{
    private readonly CadSessionService _cad;
    private readonly CadDocumentSession _session;
    private readonly CadEditorSettings _settings;
    private readonly CadCommandBus _bus;
    private readonly CadCommandDispatcher _dispatcher;
    private readonly CadToolController _tools;
    private readonly CadModelRenderer _modelRenderer;
    private CadEditorSurface _editor = null!;
    private TextBlock _status = null!;
    private TextBlock _inspector = null!;
    private int _deck;

    public MainWindow(CadSessionService cad)
    {
        _cad = cad;
        _session = cad.Document;
        _settings = cad.Settings;
        _bus = cad.Bus;
        _dispatcher = cad.Dispatcher;
        _tools = new CadToolController(_dispatcher, _settings);
        _modelRenderer = new CadModelRenderer(_session, _settings);

        Title = "Ship Designer";
        Width = 1440;
        Height = 920;

        Content = BuildLayout();
        _cad.Editor = _editor;
        _cad.ExportRoot = Path.Combine(_settings.DataRoot, "exports");
        _cad.FitHandler = () => _modelRenderer.Fit();
        _cad.ActionResult += OnActionResult;
        _session.Changed += RefreshInspector;

        if (_session.Document.Entities.Count == 0)
            _session.NewDocument();

        ShipDocumentMetrics.SetShipEnvelope(_session.Document, 65f, 20f, 12f, 4f);
        RefreshInspector();
    }

    private Control BuildLayout()
    {
        _editor = new CadEditorSurface(_session, _settings, _bus, _dispatcher, _tools, _modelRenderer);
        _status = new TextBlock
        {
            Text = "Ready",
            Margin = new Thickness(8, 4),
            Foreground = Brushes.LightGray,
        };
        _inspector = new TextBlock
        {
            FontFamily = new FontFamily("Consolas,Courier New,monospace"),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(8),
        };

        var menu = new Menu();
        var file = new MenuItem { Header = "_File" };
        file.Items.Add(MenuCmd("New", () =>
        {
            _session.NewDocument();
            ShipDocumentMetrics.SetShipEnvelope(_session.Document, 65, 20, 12, 4);
            RefreshInspector();
        }));
        file.Items.Add(MenuCmd("Open…", OnOpen));
        file.Items.Add(MenuCmd("Save", () => _cad.Execute(new CadCommandDto { ActionId = CadSessionActionIds.Save })));
        file.Items.Add(MenuCmd("Save As…", OnSaveAs));
        file.Items.Add(new Separator());
        file.Items.Add(MenuCmd("Import Calypso seed…", OnImportShip));
        file.Items.Add(new Separator());
        file.Items.Add(MenuCmd("Exit", Close));
        var ship = new MenuItem { Header = "_Ship" };
        ship.Items.Add(MenuCmd("Validate Ship",
            () => _cad.Execute(new CadCommandDto { ActionId = ShipChrome.ValidateShipActionId })));
        ship.Items.Add(MenuCmd("Refresh airtight",
            () => _cad.Execute(new CadCommandDto { ActionId = ShipChrome.RefreshAirtightActionId })));
        menu.Items.Add(file);
        menu.Items.Add(ship);

        var shipStrip = ShipChrome.CreateToolStrip(
            _cad,
            deck =>
            {
                _deck = deck;
                _settings.Settings.DrawElevation = deck * ShipDocumentMetrics.GetDeckSpacingMeters(_session.Document);
                RefreshInspector();
            },
            () => _deck);

        var workspaceBar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(8, 4),
        };
        workspaceBar.Children.Add(Ws("GA", "General Arrangement", isolate: true));
        workspaceBar.Children.Add(Ws("Pressure", "Pressure & Hatches", isolate: true));
        workspaceBar.Children.Add(Ws("Hull", "Hull & Exterior", isolate: false));
        workspaceBar.Children.Add(Ws("Preview", "Preview", isolate: false));

        var right = new ScrollViewer
        {
            Width = 320,
            Content = _inspector,
            Background = new SolidColorBrush(Color.Parse("#1a1c20")),
        };

        var body = new DockPanel();
        DockPanel.SetDock(menu, Dock.Top);
        DockPanel.SetDock(shipStrip, Dock.Top);
        DockPanel.SetDock(workspaceBar, Dock.Top);
        DockPanel.SetDock(_status, Dock.Bottom);
        DockPanel.SetDock(right, Dock.Right);
        body.Children.Add(menu);
        body.Children.Add(shipStrip);
        body.Children.Add(workspaceBar);
        body.Children.Add(_status);
        body.Children.Add(right);
        body.Children.Add(_editor);
        return body;
    }

    private Button Ws(string label, string title, bool isolate)
    {
        var btn = new Button { Content = label, Padding = new Thickness(10, 4) };
        btn.Click += (_, _) =>
        {
            Title = $"Ship Designer — {title}";
            _status.Text = title;
            _settings.Settings.IsolateLevel = isolate;
            RefreshInspector();
        };
        return btn;
    }

    private static MenuItem MenuCmd(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    private async void OnOpen()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open ship .cadjson",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Cad JSON") { Patterns = ["*.cadjson"] },
            ],
        });
        if (files.Count == 0)
            return;
        var path = files[0].TryGetLocalPath();
        if (path is null)
            return;
        _cad.Execute(new CadCommandDto { ActionId = CadSessionActionIds.Open, Path = path });
        RefreshInspector();
    }

    private async void OnSaveAs()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save ship .cadjson",
            DefaultExtension = "cadjson",
            FileTypeChoices =
            [
                new FilePickerFileType("Cad JSON") { Patterns = ["*.cadjson"] },
            ],
        });
        var path = file?.TryGetLocalPath();
        if (path is null)
            return;
        _session.SaveTo(path);
        _status.Text = $"Saved {path}";
    }

    private void OnImportShip()
    {
        var result = _cad.Execute(new CadCommandDto { ActionId = CadShipChrome.ImportShipActionId });
        _status.Text = result.Message ?? (result.Ok ? "Imported" : "Import failed");
        RefreshInspector();
    }

    private void OnActionResult(CadActionResultEventDto e) =>
        _status.Text = string.IsNullOrWhiteSpace(e.Message) ? e.ActionId : e.Message;

    private void RefreshInspector() =>
        _inspector.Text = ShipInspectorText.Format(_cad);
}
