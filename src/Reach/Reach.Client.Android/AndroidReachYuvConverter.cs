using Android.Media;
using Novolis.Reach.Client;
using Novolis.Video;
using System.Threading.Tasks;

namespace Novolis.Reach.Client.Android;

internal static class AndroidReachYuvConverter
{
    internal static RawVideoFrame? ConvertToBgra(
        Image image,
        int width,
        int height,
        long timestamp)
    {
        var planes = image.GetPlanes();
        if (planes is null || planes.Length < 3)
            return null;

        var yPlane = CopyPlane(planes[0]);
        var uPlane = CopyPlane(planes[1]);
        var vPlane = CopyPlane(planes[2]);
        var pixels = new byte[checked(width * height * 4)];
        var outputStride = checked(width * 4);
        var options = new ParallelOptions
        {
            // Leave a core for the Android UI and decoder threads. Saturating
            // the emulator with conversion workers turns frame pressure into
            // an application-not-responding state.
            MaxDegreeOfParallelism = System.Math.Min(2, System.Math.Max(1, Environment.ProcessorCount)),
        };
        Parallel.For(
            0,
            height,
            options,
            y =>
            {
                var yOffset = y * yPlane.RowStride;
                var chromaY = y / 2;
                var chromaOffset = chromaY * uPlane.RowStride;
                var vChromaOffset = chromaY * vPlane.RowStride;
                var outputOffset = y * outputStride;
                for (var x = 0; x < width; x++)
                {
                    var chromaX = x / 2;
                    var yIndex = yOffset + x * yPlane.PixelStride;
                    var uIndex = chromaOffset + chromaX * uPlane.PixelStride;
                    var vIndex = vChromaOffset + chromaX * vPlane.PixelStride;
                    var yValue = Read(yPlane.Bytes, yIndex);
                    var uValue = Read(uPlane.Bytes, uIndex);
                    var vValue = Read(vPlane.Bytes, vIndex);

                    var c = System.Math.Max(0, yValue - 16);
                    var red = Clamp((298 * c + 409 * (vValue - 128) + 128) >> 8);
                    var green = Clamp(
                        (298 * c
                         - 100 * (uValue - 128)
                         - 208 * (vValue - 128)
                         + 128) >> 8);
                    var blue = Clamp((298 * c + 516 * (uValue - 128) + 128) >> 8);

                    var pixel = outputOffset + x * 4;
                    pixels[pixel] = (byte)blue;
                    pixels[pixel + 1] = (byte)green;
                    pixels[pixel + 2] = (byte)red;
                    pixels[pixel + 3] = byte.MaxValue;
                }
            });

        ReachVideoFrameOrientation.NormalizeTopToBottom(pixels, width, height);
        return new RawVideoFrame(
            width,
            height,
            checked(width * 4),
            VideoPixelFormat.Bgra32,
            pixels,
            timestamp);
    }

    private static PlaneData CopyPlane(Image.Plane plane)
    {
        var buffer = plane.Buffer
            ?? throw new InvalidOperationException(
                "Android decoder returned an empty image plane.");
        using var source = buffer.Duplicate();
        var bytes = new byte[source.Remaining()];
        source.Get(bytes);
        return new PlaneData(bytes, plane.RowStride, plane.PixelStride);
    }

    private static int Read(byte[] bytes, int index) =>
        index >= 0 && index < bytes.Length
            ? bytes[index]
            : 128;

    private static int Clamp(int value) => System.Math.Clamp(value, 0, 255);

    private readonly record struct PlaneData(
        byte[] Bytes,
        int RowStride,
        int PixelStride)
    {
    }
}
