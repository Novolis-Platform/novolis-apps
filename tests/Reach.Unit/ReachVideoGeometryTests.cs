using Novolis.Reach.Client;

namespace Reach.Unit;

public sealed class ReachVideoGeometryTests
{
    [Test]
    public async Task CenterPointMapsIntoTheSelectedMonitorOrigin()
    {
        var fit = ReachVideoGeometry.CalculateFit(
            surfaceWidth: 800,
            surfaceHeight: 600,
            videoWidth: 1920,
            videoHeight: 1080,
            zoom: 1,
            panX: 0,
            panY: 0);

        var mapped = ReachVideoGeometry.TryMapPoint(
            fit,
            pointX: 400,
            pointY: 300,
            videoWidth: 1920,
            videoHeight: 1080,
            displayLeft: 1920,
            displayTop: 100,
            displayWidth: 1920,
            displayHeight: 1080,
            out var sourceX,
            out var sourceY);

        await Assert.That(mapped).IsTrue();
        await Assert.That(sourceX).IsEqualTo(2880).Within(1e-9);
        await Assert.That(sourceY).IsEqualTo(640).Within(1e-9);
    }

    [Test]
    public async Task PointsOutsideTheFittedVideoAreRejected()
    {
        var fit = ReachVideoGeometry.CalculateFit(
            surfaceWidth: 800,
            surfaceHeight: 600,
            videoWidth: 1920,
            videoHeight: 1080,
            zoom: 1,
            panX: 0,
            panY: 0);

        var mapped = ReachVideoGeometry.TryMapPoint(
            fit,
            pointX: 10,
            pointY: 10,
            videoWidth: 1920,
            videoHeight: 1080,
            displayLeft: 0,
            displayTop: 0,
            displayWidth: 1920,
            displayHeight: 1080,
            out _,
            out _);

        await Assert.That(mapped).IsFalse();
    }
}
