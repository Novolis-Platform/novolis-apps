using System.Numerics;
using ConceptStudio.Models;
using ConceptStudio.Services;
using Novolis.Rendering.PathTrace.Demos;
using Novolis.Rendering.Runtime;

namespace ConceptStudio.Services;

internal static class ConceptQualityExporter
{
    public static void ExportPerspectivePng(
        ConceptSceneStore store,
        ConceptDocument document,
        int revision,
        OrbitCameraState cameraState,
        string path,
        int width = 1280,
        int height = 720,
        int samples = 48)
    {
        var compiled = store.Compile(document, revision);
        using var session = new PathTraceSession(compiled);
        session.Resize(width, height);

        var orbit = new Novolis.Rendering.Presentation.Silk.SilkOrbitCamera
        {
            Yaw = cameraState.Yaw,
            Pitch = cameraState.Pitch,
            Distance = cameraState.Distance,
            Target = cameraState.Target.Length >= 3
                ? new Vector3(cameraState.Target[0], cameraState.Target[1], cameraState.Target[2])
                : new Vector3(0f, 1f, 0f),
        };

        var eye = orbit.BuildEyePosition();
        var camera = CameraSnapshot.LookAt(eye, orbit.Target, Vector3.UnitY, orbit.FieldOfViewDegrees, width / (float)height);

        session.Backend.ResetAccumulation();
        for (var s = 0; s < samples; s++)
            session.Backend.RenderAsync(camera, s).GetAwaiter().GetResult();

        if (!session.Backend.Output.TryGetCpuPixels(out var pixels, out var w, out var h))
            throw new InvalidOperationException("Path trace backend did not produce CPU pixels.");

        ConceptPngExporter.SaveRgba(path, pixels.ToArray(), w, h);
    }
}
