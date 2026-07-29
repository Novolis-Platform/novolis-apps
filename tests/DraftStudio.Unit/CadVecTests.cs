using DraftStudio.Models;

namespace DraftStudio.Unit;

public sealed class CadVecTests
{
    [Test]
    public async Task Plan_Sets_Elevation_As_Y()
    {
        var p = CadVec.Plan(1, 2, 3.5f);
        await Assert.That(p[0]).IsEqualTo(1f);
        await Assert.That(p[1]).IsEqualTo(3.5f);
        await Assert.That(p[2]).IsEqualTo(2f);
    }

    [Test]
    public async Task ElevationOf_Line_Uses_A()
    {
        var line = new CadEntity
        {
            Kind = "line",
            A = CadVec.Plan(0, 0, 2f),
            B = CadVec.Plan(1, 1, 2f),
        };
        await Assert.That(CadVec.ElevationOf(line)).IsEqualTo(2f);
    }

    [Test]
    public async Task MatchesLevel_Respects_Tolerance()
    {
        var circle = new CadEntity
        {
            Kind = "circle",
            Center = CadVec.Plan(0, 0, 3.02f),
            Radius = 1,
        };
        await Assert.That(CadVec.MatchesLevel(circle, 3f, 0.05f)).IsTrue();
        await Assert.That(CadVec.MatchesLevel(circle, 3f, 0.01f)).IsFalse();
    }

    [Test]
    public async Task TranslateEntity_Moves_Line_Endpoints()
    {
        var line = new CadEntity
        {
            Kind = "line",
            A = CadVec.Xyz(0, 0, 0),
            B = CadVec.Xyz(1, 0, 0),
        };
        CadVec.TranslateEntity(line, 2, 1, 3);
        await Assert.That(line.A![0]).IsEqualTo(2f);
        await Assert.That(line.A[1]).IsEqualTo(1f);
        await Assert.That(line.B![2]).IsEqualTo(3f);
    }
}
