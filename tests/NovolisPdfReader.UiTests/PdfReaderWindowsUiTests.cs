using Novolis.Maui.Agent.Protocol;
using Novolis.Testing.Appium;
using OpenQA.Selenium.Appium;
using TUnit.Core;

namespace NovolisPdfReader.UiTests;

public sealed class PdfReaderWindowsUiTests
{
    [Test]
    public async Task LaunchesChromeAndMauiAgent()
    {
        Skip.Unless(OperatingSystem.IsWindows(), "Windows Appium driver is required.");
        Skip.Unless(PdfReaderUiHarness.AppiumIsListening(), "Start Appium on APPIUM_HOST (default http://127.0.0.1:4723/).");
        var exe = PdfReaderUiHarness.TryResolveWindowsExe();
        Skip.Unless(exe is not null, "Build NovolisPdfReader or set NOVOLIS_PDFREADER_UI_APP to the unpackaged exe.");

        using var session = WindowsAppiumSession.Connect(new WindowsAppiumSessionOptions
        {
            App = exe!,
        });

        var open = session.Driver.FindElement(MobileBy.AccessibilityId("PdfReaderHeroOpen"));
        var documentName = session.Driver.FindElement(MobileBy.AccessibilityId("PdfReaderDocumentName"));
        var welcome = session.Driver.FindElement(MobileBy.AccessibilityId("PdfReaderWelcome"));

        await Assert.That(open.Displayed).IsTrue();
        await Assert.That(documentName.Displayed).IsTrue();
        await Assert.That(documentName.Text).Contains("No document open");
        await Assert.That(welcome.Displayed).IsTrue();

        await using var client = new UiAgentClient();
        Exception? last = null;
        for (var attempt = 1; attempt <= 20; attempt++)
        {
            try
            {
                await client.ConnectDefaultAsync().ConfigureAwait(false);
                last = null;
                break;
            }
            catch (Exception exception)
            {
                last = exception;
                await Task.Delay(250).ConfigureAwait(false);
            }
        }

        await Assert.That(client.IsConnected).IsTrue().Because(last?.Message ?? "MAUI agent pipe did not accept.");
        var hello = await client.HelloAsync().ConfigureAwait(false);
        await Assert.That(hello.Success).IsTrue().Because(hello.Error ?? "hello failed");
        await Assert.That(hello.AppTitle).Contains("PDF Reader");

        var tree = await client.TreeAsync(interactiveOnly: true).ConfigureAwait(false);
        await Assert.That(tree.Success).IsTrue().Because(tree.Error ?? "tree failed");
        var ids = tree.Nodes.Select(static node => node.Id).ToHashSet(StringComparer.Ordinal);
        await Assert.That(ids.Contains("PdfReaderHeroOpen")).IsTrue();
    }

    [Test]
    public async Task OpensDocumentChromeWhenCalypsoFileIsPresent()
    {
        Skip.Unless(OperatingSystem.IsWindows(), "Windows Appium driver is required.");
        Skip.Unless(PdfReaderUiHarness.AppiumIsListening(), "Start Appium on APPIUM_HOST (default http://127.0.0.1:4723/).");
        var exe = PdfReaderUiHarness.TryResolveWindowsExe();
        Skip.Unless(exe is not null, "Build NovolisPdfReader or set NOVOLIS_PDFREADER_UI_APP to the unpackaged exe.");
        var pdf = PdfReaderUiHarness.TryResolveCalypsoPdf();
        Skip.Unless(
            pdf is not null,
            "Print Calypso with manuscript-cli into D:\\repos\\books\\out\\the-calypso-cycle\\calypso\\calypso.pdf (or set NOVOLIS_BOOKS_PDF / NOVOLIS_BOOKS_ROOT).");

        using var session = WindowsAppiumSession.Connect(new WindowsAppiumSessionOptions
        {
            App = exe!,
            AppArguments = $"\"{pdf}\"",
        });

        session.Driver.FindElement(MobileBy.AccessibilityId("PdfPageRail"));
        session.Driver.FindElement(MobileBy.AccessibilityId("PdfPageEntry"));
        var pageLabel = session.Driver.FindElement(MobileBy.AccessibilityId("PdfPageLabel"));
        await Assert.That(pageLabel.Displayed).IsTrue();
        await Assert.That(pageLabel.Text).Contains("/");
    }

    [Test]
    public async Task PasswordProtectedDocumentExposesUnlockAndCancelControls()
    {
        Skip.Unless(OperatingSystem.IsWindows(), "Windows Appium driver is required.");
        Skip.Unless(PdfReaderUiHarness.AppiumIsListening(), "Start Appium on APPIUM_HOST (default http://127.0.0.1:4723/).");
        var exe = PdfReaderUiHarness.TryResolveWindowsExe();
        Skip.Unless(exe is not null, "Build NovolisPdfReader or set NOVOLIS_PDFREADER_UI_APP to the unpackaged exe.");
        var pdf = PdfReaderUiHarness.TryResolveProtectedCompatibilityPdf();
        Skip.Unless(pdf is not null, "Generate the required protected compatibility fixture first.");

        using var session = WindowsAppiumSession.Connect(new WindowsAppiumSessionOptions
        {
            App = exe!,
            AppArguments = $"\"{pdf}\"",
        });

        var prompt = session.Driver.FindElement(MobileBy.AccessibilityId("PdfReaderPasswordPrompt"));
        var entry = session.Driver.FindElement(MobileBy.AccessibilityId("PdfReaderPassword"));
        var cancel = session.Driver.FindElement(MobileBy.AccessibilityId("PdfReaderPasswordCancel"));

        await Assert.That(prompt.Displayed).IsTrue();
        await Assert.That(entry.Displayed).IsTrue();
        await Assert.That(cancel.Displayed).IsTrue();
        cancel.Click();
    }
}
