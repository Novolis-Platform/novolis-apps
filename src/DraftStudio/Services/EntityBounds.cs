using System.Numerics;
using DraftStudio.Models;

namespace DraftStudio.Services;

internal static class EntityBounds
{
    public static (Vector3 Center, float Radius) Compute(CadDocument document)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        var any = false;

        foreach (var entity in document.Entities)
        {
            foreach (var p in CadVec.EnumerateWorldPoints(entity))
            {
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
                any = true;
            }
        }

        if (!any)
            return (Vector3.Zero, 5f);

        var center = (min + max) * 0.5f;
        var radius = Math.Max(0.5f, (max - min).Length() * 0.5f);
        return (center, radius);
    }
}
