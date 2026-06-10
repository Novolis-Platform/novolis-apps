using Novolis.Math.Geometry;
using SixLabors.ImageSharp;
using SixLaborsImageSharpPixelFormats = SixLabors.ImageSharp.PixelFormats;

namespace ConceptStudio.Services;

internal static class ConceptPngExporter
{
    public static void SaveRgba(string path, Rgba32[] pixels, int width, int height)
    {
        var buffer = new SixLaborsImageSharpPixelFormats.Rgba32[pixels.Length];
        for (var i = 0; i < pixels.Length; i++)
            buffer[i] = new SixLaborsImageSharpPixelFormats.Rgba32(pixels[i].R, pixels[i].G, pixels[i].B, pixels[i].A);

        using var image = Image.LoadPixelData(buffer, width, height);
        image.SaveAsPng(path);
    }
}
