using Novolis.Avalonia.Cad.Core;

namespace DraftStudio.Unit;

public sealed class ArtifactDumperPathTests
{
    [Test]
    public async Task AllocatePngPath_Is_Under_Dumps()
    {
        var root = Path.Combine(Path.GetTempPath(), "draft-art-" + Guid.NewGuid().ToString("N"));
        var settings = new CadEditorSettings(root);
        var session = new CadDocumentSession(settings);
        var dumper = new DraftStudio.Services.DraftArtifactDumper(session, settings);
        var path = dumper.AllocatePngPath("model");
        await Assert.That(path).Contains("dumps");
        await Assert.That(path).Contains("model-");
        await Assert.That(path.EndsWith(".png")).IsTrue();
    }
}
