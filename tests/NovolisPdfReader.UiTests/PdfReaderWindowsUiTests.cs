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

        var open = session.Driver.FindElement(MobileBy.AccessibilityId("PdfReaderOpen"));
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
        await Assert.That(ids.Contains("PdfReaderOpen")).IsTrue();
        await Assert.That(ids.Contains("PdfReaderHeroOpen")).IsTrue();
    }
}
