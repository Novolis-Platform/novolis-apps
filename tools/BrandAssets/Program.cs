using System.Buffers.Binary;
using SkiaSharp;
using Svg.Skia;

namespace Novolis.Apps.BrandAssets;

internal static class Program
{
    private static readonly SKColor Canvas = SKColor.Parse("#111827");

    private static int Main(string[] args)
    {
        var repoRoot = args.Length > 0
            ? Path.GetFullPath(args[0])
            : FindRepoRoot();

        var svgPath = Path.Combine(repoRoot, "logo-icon.svg");
        if (!File.Exists(svgPath))
            svgPath = Path.Combine(repoRoot, "brand", "logo-icon.svg");
        if (!File.Exists(svgPath))
        {
            Console.Error.WriteLine($"Brand SVG not found under {repoRoot}.");
            return 1;
        }

        var brandDir = Path.Combine(repoRoot, "brand");
        var androidDir = Path.Combine(brandDir, "android");
        Directory.CreateDirectory(androidDir);

        File.Copy(svgPath, Path.Combine(brandDir, "logo-icon.svg"), overwrite: true);

        using var svg = new SKSvg();
        svg.Load(svgPath);
        var picture = svg.Picture ?? throw new InvalidOperationException($"Failed to load SVG: {svgPath}");

        WritePng(picture, Path.Combine(repoRoot, "icon.png"), 512, Canvas, 0.10f);
        WritePng(picture, Path.Combine(androidDir, "ic_launcher.png"), 192, Canvas, 0.10f);
        WriteIco(
            picture,
            Path.Combine(repoRoot, "icon.ico"),
            [16, 24, 32, 48, 64, 128, 256]);

        var merglyphIconDir = Path.Combine(repoRoot, "src", "Merglyph", "Merglyph", "Resources", "AppIcon");
        var merglyphSplashDir = Path.Combine(repoRoot, "src", "Merglyph", "Merglyph", "Resources", "Splash");
        Directory.CreateDirectory(merglyphIconDir);
        Directory.CreateDirectory(merglyphSplashDir);
        WriteSolidPng(Path.Combine(merglyphIconDir, "appicon.png"), 1024, Canvas);
        WritePng(picture, Path.Combine(merglyphIconDir, "appiconfg.png"), 1024, SKColors.Transparent, 0.18f);
        WritePng(picture, Path.Combine(merglyphSplashDir, "splash.png"), 1024, SKColors.Transparent, 0.18f);

        Console.WriteLine($"Wrote brand rasters from {svgPath}");
        return 0;
    }

    private static void WritePng(SKPicture picture, string path, int size, SKColor background, float padFraction)
    {
        using var bitmap = Render(picture, size, background, padFraction);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException($"PNG encode failed for {path}.");
        File.WriteAllBytes(path, data.ToArray());
        Console.WriteLine($"  {path} ({size}x{size})");
    }

    private static void WriteSolidPng(string path, int size, SKColor color)
    {
        using var bitmap = new SKBitmap(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(color);
        canvas.Flush();
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException($"PNG encode failed for {path}.");
        File.WriteAllBytes(path, data.ToArray());
        Console.WriteLine($"  {path} ({size}x{size} solid)");
    }

    private static void WriteIco(SKPicture picture, string path, int[] sizes)
    {
        var images = new List<byte[]>(sizes.Length);
        foreach (var size in sizes)
        {
            using var bitmap = Render(picture, size, Canvas, 0.10f);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100)
                ?? throw new InvalidOperationException($"ICO PNG encode failed at {size}.");
            images.Add(data.ToArray());
        }

        File.WriteAllBytes(path, PackIco(images, sizes));
        Console.WriteLine($"  {path} ({string.Join(',', sizes)})");
    }

    private static SKBitmap Render(SKPicture picture, int size, SKColor background, float padFraction)
    {
        var bitmap = new SKBitmap(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(background);

        var bounds = picture.CullRect;
        var pad = size * padFraction;
        var inner = size - (pad * 2);
        var scale = Math.Min(inner / bounds.Width, inner / bounds.Height);
        var dx = ((size - (bounds.Width * scale)) / 2f) - (bounds.Left * scale);
        var dy = ((size - (bounds.Height * scale)) / 2f) - (bounds.Top * scale);
        canvas.Translate(dx, dy);
        canvas.Scale(scale);
        canvas.DrawPicture(picture);
        canvas.Flush();
        return bitmap;
    }

    private static byte[] PackIco(IReadOnlyList<byte[]> pngs, IReadOnlyList<int> sizes)
    {
        const int headerSize = 6;
        const int entrySize = 16;
        var offset = headerSize + (entrySize * pngs.Count);
        var total = offset + pngs.Sum(p => p.Length);
        var buffer = new byte[total];
        var span = buffer.AsSpan();

        BinaryPrimitives.WriteUInt16LittleEndian(span[0..], 0);
        BinaryPrimitives.WriteUInt16LittleEndian(span[2..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(span[4..], (ushort)pngs.Count);

        var cursor = headerSize;
        var imageOffset = offset;
        for (var i = 0; i < pngs.Count; i++)
        {
            var size = sizes[i];
            var png = pngs[i];
            span[cursor] = size >= 256 ? (byte)0 : (byte)size;
            span[cursor + 1] = size >= 256 ? (byte)0 : (byte)size;
            span[cursor + 2] = 0;
            span[cursor + 3] = 0;
            BinaryPrimitives.WriteUInt16LittleEndian(span[(cursor + 4)..], 1);
            BinaryPrimitives.WriteUInt16LittleEndian(span[(cursor + 6)..], 32);
            BinaryPrimitives.WriteUInt32LittleEndian(span[(cursor + 8)..], (uint)png.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(span[(cursor + 12)..], (uint)imageOffset);
            png.CopyTo(buffer.AsSpan(imageOffset));
            imageOffset += png.Length;
            cursor += entrySize;
        }

        return buffer;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "build", "apps.json")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not find novolis-apps repository root.");
    }
}
