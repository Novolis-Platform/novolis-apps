using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Novolis.Avalonia.GraphicalProfile;
using Novolis.Avalonia.Navigation;
using Novolis.Commands.Navigation;
using Sudoku.App;
using Microsoft.Extensions.DependencyInjection;

namespace MobileUxCapture;

/// <summary>Sudoku, PDF, Presence, and catalog chrome — full theme × form matrix.</summary>
internal static class ShellUxCapture
{
    public static void CaptureSudoku(string outDir)
    {
        UxCaptureSession.ForEachThemeAndForm(
            outDir,
            "sudoku",
            "board",
            (form, theme) =>
            {
                // Isolated storage per frame so status shows ladder start, not a resume.
                var themeId = theme == ThemeVariant.Dark ? "dark" : "light";
                var storage = Path.Combine(
                    Path.GetTempPath(),
                    "novolis-ux-sudoku",
                    $"{form.Id}-{themeId}");
                if (Directory.Exists(storage))
                    Directory.Delete(storage, recursive: true);
                Directory.CreateDirectory(storage);
                var services = new ServiceCollection()
                    .AddSudokuApp(storage)
                    .BuildServiceProvider();
                return new SudokuView(services.GetRequiredService<SudokuGameService>());
            },
            settleMs: 700);
    }

    public static void CapturePresence(string outDir)
    {
        foreach (var route in new[] { "today", "map", "places", "report" })
        {
            UxCaptureSession.ForEachThemeAndForm(
                outDir,
                "presence",
                route,
                (_, _) => BuildPresenceShell(route));
        }
    }

    public static void CapturePdf(string outDir)
    {
        UxCaptureSession.ForEachThemeAndForm(
            outDir,
            "pdf",
            "welcome",
            (_, _) => BuildPdfWelcome());

        // Explicit before/after regression still phone-sized; emit both themes.
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            Application.Current!.RequestedThemeVariant = theme;
            var themeId = theme == ThemeVariant.Dark ? "dark" : "light";
            UxCaptureSession.CaptureControl(
                BuildPdfSliver(before: true),
                UxFormFactor.Phone.Width,
                UxFormFactor.Phone.Height,
                Path.Combine(outDir, UxCaptureNames.File("pdf", "before-sliver", "phone", themeId)));
            UxCaptureSession.CaptureControl(
                BuildPdfSliver(before: false),
                UxFormFactor.Phone.Width,
                UxFormFactor.Phone.Height,
                Path.Combine(outDir, UxCaptureNames.File("pdf", "after-welcome", "phone", themeId)));
        }
    }

    /// <summary>
    /// Catalog chrome for remaining novolis-apps hosts — title + body note —
    /// so the eval matrix has light/dark × phone/desktop for every app name.
    /// </summary>
    public static void CaptureAppCatalog(string outDir)
    {
        foreach (var app in CatalogApps)
        {
            UxCaptureSession.ForEachThemeAndForm(
                outDir,
                app.Id,
                "chrome",
                (_, _) => BuildCatalogChrome(app));
        }
    }

    static readonly CatalogApp[] CatalogApps =
    [
        new("assetstudio", "Asset Studio", "Desktop asset pipeline host."),
        new("bookswriter", "Books Writer Studio", "Wide authoring sibling of Books Mobile."),
        new("cadstudio", "CAD Studio", "Ship / CAD Avalonia host."),
        new("frankmoat", "Frank Moat", "Moat product host."),
        new("httpstudio", "HTTP Studio", "HTTP inspection studio."),
        new("livestudio", "Live Studio", "Live composition host."),
        new("merglyph", "Merglyph", "MAUI island — chrome catalog only."),
        new("ndjsonviewer", "NDJSON Viewer", "NDJSON stream viewer."),
        new("nmule", "nMule", "Utility host."),
        new("maps", "Novolis Maps", "Map product host."),
        new("quietvideo", "Quiet Video", "Video utility."),
        new("torrent", "Novolis Torrent", "Torrent session chrome."),
        new("querystudio", "Query Studio", "Query / data studio."),
        new("reach", "Reach", "Remote desktop host."),
        new("robosharp", "RoboSharp Studio", "RoboSharp studio."),
        new("sins", "Sins of a Capitalism Tycoon", "Game host."),
        new("sketchstudio", "Sketch Studio", "Sketch authoring host."),
    ];

    static Control BuildPresenceShell(string activeRoute)
    {
        var tree = new MobileMenuTree(
        [
            Node("today", "Today"),
            Node("map", "Map"),
            Node("places", "Places"),
            Node("report", "Report"),
            new MobileMenuNode("settings", "Settings", MobileMenuNodeKind.Action)
            {
                CommandId = "settings",
            },
        ]);
        var navigation = new MobileNavigationController(tree);
        navigation.Activate(activeRoute);
        var body = new Border
        {
            Background = GraphicalProfile.SurfaceBrush,
            Padding = new Thickness(16),
            Child = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    Eyebrow("YOUR DAY"),
                    Title("Presence Ledger"),
                    Muted($"Active route · {activeRoute}. Primary tabs stay single-line; body fills remaining height."),
                    Card($"Observation · live · route {activeRoute}"),
                },
            },
        };
        return new MobileNavigationShell(navigation)
        {
            Title = "Presence Ledger",
            Body = body,
        };

        static MobileMenuNode Node(string id, string title) =>
            new(id, title)
            {
                Placement = MobileMenuPlacement.Primary,
                RouteId = id,
            };
    }

    static Control BuildPdfWelcome()
    {
        var welcome = new Border
        {
            Background = GraphicalProfile.SurfaceBrush,
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(18),
            Margin = new Thickness(16),
            Child = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    Eyebrow("OFFLINE PDF READER"),
                    Title("Read without sending your files away."),
                    Muted("Welcome fills the body on phone widths instead of an empty viewer strip."),
                    PrimaryButton("Open a PDF document"),
                },
            },
        };
        var tree = new MobileMenuTree(
        [
            new MobileMenuNode("pdf.open", "Open PDF", MobileMenuNodeKind.Action)
            {
                CommandId = "pdf.open",
            },
        ]);
        return new MobileNavigationShell(new MobileNavigationController(tree))
        {
            Title = "PDF Reader",
            Body = welcome,
        };
    }

    static Control BuildPdfSliver(bool before)
    {
        Control body;
        if (before)
        {
            body = new StackPanel
            {
                Spacing = 8,
                Margin = new Thickness(16),
                Children =
                {
                    Eyebrow("BEFORE · empty compact viewer"),
                    Card("Viewer header"),
                    Card("Toolbar"),
                    Muted("Star workspace collapsed to sliver"),
                },
            };
        }
        else
        {
            body = new Border
            {
                Background = GraphicalProfile.SurfaceBrush,
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(18),
                Margin = new Thickness(16),
                Child = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        Eyebrow("AFTER · welcome fills body"),
                        Title("Welcome / document body"),
                        Muted("Open · search · read locally"),
                        PrimaryButton("Open a PDF document"),
                    },
                },
            };
        }

        var tree = new MobileMenuTree(
        [
            new MobileMenuNode("more", "More", MobileMenuNodeKind.Action) { CommandId = "more" },
        ]);
        return new MobileNavigationShell(new MobileNavigationController(tree))
        {
            Title = "PDF Reader",
            Body = body,
        };
    }

    static Control BuildCatalogChrome(CatalogApp app)
    {
        var tree = new MobileMenuTree(
        [
            new MobileMenuNode("home", "Home")
            {
                Placement = MobileMenuPlacement.Primary,
                RouteId = "home",
            },
            new MobileMenuNode("work", "Work")
            {
                Placement = MobileMenuPlacement.Primary,
                RouteId = "work",
            },
            new MobileMenuNode("more", "More", MobileMenuNodeKind.Action)
            {
                CommandId = "more",
            },
        ]);
        var body = new Border
        {
            Background = GraphicalProfile.SurfaceBrush,
            Padding = new Thickness(16),
            Child = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    Eyebrow("NOVOLIS APPS CATALOG"),
                    Title(app.Title),
                    Muted(app.Blurb),
                    Card("Chrome baseline · product views not headless-captured in this pass."),
                },
            },
        };
        return new MobileNavigationShell(new MobileNavigationController(tree))
        {
            Title = app.Title,
            Body = body,
        };
    }

    static TextBlock Eyebrow(string text) =>
        new()
        {
            Text = text,
            Classes = { "eyebrow" },
            Foreground = GraphicalProfile.AccentBrush,
            FontWeight = FontWeight.Bold,
        };

    static TextBlock Title(string text) =>
        new()
        {
            Text = text,
            FontSize = 26,
            FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap,
            Foreground = GraphicalProfile.TextBrush,
        };

    static TextBlock Muted(string text) =>
        new()
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = GraphicalProfile.MutedBrush,
        };

    static Border Card(string text) =>
        new()
        {
            Background = GraphicalProfile.RaisedBrush,
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(14),
            Child = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                Foreground = GraphicalProfile.TextBrush,
            },
        };

    static Button PrimaryButton(string text) =>
        new()
        {
            Content = text,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 44,
            Background = GraphicalProfile.AccentFillBrush,
            Foreground = GraphicalProfile.OnAccentFillBrush,
        };

    readonly record struct CatalogApp(string Id, string Title, string Blurb);
}
