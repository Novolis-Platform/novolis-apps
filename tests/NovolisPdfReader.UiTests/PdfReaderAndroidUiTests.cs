using Novolis.Testing.Appium;
using OpenQA.Selenium.Appium;
using TUnit.Core;

namespace NovolisPdfReader.UiTests;

public sealed class PdfReaderAndroidUiTests
{
    [Test]
    public async Task LaunchesWelcomeChrome()
    {
        Skip.Unless(PdfReaderUiHarness.IsAndroidRequested(), "Set NOVOLIS_PDFREADER_UI_PLATFORM=android to run.");
        Skip.Unless(PdfReaderUiHarness.AppiumIsListening(), "Start Appium on APPIUM_HOST (default http://127.0.0.1:4723/).");
        var apk = PdfReaderUiHarness.TryResolveAndroidApk();
        Skip.Unless(apk is not null, "Set NOVOLIS_PDFREADER_UI_APP to the built APK.");

        using var session = AndroidAppiumSession.Connect(new AndroidAppiumSessionOptions
        {
            AppPath = apk,
            AppPackage = "com.novolis.pdfreader",
            AppActivity = "NovolisPdfReader.MainActivity",
            NoReset = true,
        });

        var open = session.Driver.FindElement(MobileBy.Id("PdfReaderOpen"));
        var documentName = session.Driver.FindElement(MobileBy.Id("PdfReaderDocumentName"));

        await Assert.That(open.Displayed).IsTrue();
        await Assert.That(documentName.Displayed).IsTrue();
        await Assert.That(documentName.Text).Contains("No document open");
    }

    [Test]
    public async Task ExposesReaderChromeIds()
    {
        Skip.Unless(PdfReaderUiHarness.IsAndroidRequested(), "Set NOVOLIS_PDFREADER_UI_PLATFORM=android to run.");
        Skip.Unless(PdfReaderUiHarness.AppiumIsListening(), "Start Appium on APPIUM_HOST (default http://127.0.0.1:4723/).");
        var apk = PdfReaderUiHarness.TryResolveAndroidApk();
        Skip.Unless(apk is not null, "Set NOVOLIS_PDFREADER_UI_APP to the built APK.");

        using var session = AndroidAppiumSession.Connect(new AndroidAppiumSessionOptions
        {
            AppPath = apk,
            AppPackage = "com.novolis.pdfreader",
            AppActivity = "NovolisPdfReader.MainActivity",
            NoReset = true,
        });

        var open = session.Driver.FindElement(MobileBy.Id("PdfReaderOpen"));
        var pageEntry = session.Driver.FindElement(MobileBy.Id("PdfPageEntry"));
        var rail = session.Driver.FindElement(MobileBy.Id("PdfPageRail"));
        await Assert.That(open).IsNotNull();
        await Assert.That(pageEntry).IsNotNull();
        await Assert.That(rail).IsNotNull();
    }
}
