using TUnit.Core;

namespace Merglyph.UiTests;

public sealed class AppLaunchSmokeTests
{
    [Test]
    public async Task AppLaunchesAndExposesMainSurface()
    {
        using var session = AppiumSession.StartFromEnvironment();

        var openDocument = session.FindByAutomationId("OpenDocument");
        var documentName = session.FindByAutomationId("DocumentName");
        var documentViewer = session.FindByAutomationId("DocumentViewer");

        await Assert.That(openDocument.Displayed).IsTrue();
        await Assert.That(documentName.Displayed).IsTrue();
        await Assert.That(documentName.Text).IsEqualTo("No document open");
        await Assert.That(documentViewer.Displayed).IsTrue();
    }
}
