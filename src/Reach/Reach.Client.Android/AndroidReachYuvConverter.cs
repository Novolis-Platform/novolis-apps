using Android.Media;
using Novolis.Reach.Client;
using Novolis.Video;

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

        for (var y = 0; y < height; y++)
        {
            var chromaY = y / 2;
            for (var x = 0; x < width; x++)
            {
                var chromaX = x / 2;
                var yValue = yPlane.Read(y, x);
                var uValue = uPlane.Read(chromaY, chromaX);
                var vValue = vPlane.Read(chromaY, chromaX);

                var c = global::System.Math.Max(0, yValue - 16);
                var red = Clamp((298 * c + 409 * (vValue - 128) + 128) >> 8);
                var green = Clamp(
                    (298 * c
                     - 100 * (uValue - 128)
                     - 208 * (vValue - 128)
                     + 128) >> 8);
                var blue = Clamp((298 * c + 516 * (uValue - 128) + 128) >> 8);

                var pixel = (y * width + x) * 4;
                pixels[pixel] = (byte)blue;
                pixels[pixel + 1] = (byte)green;
                pixels[pixel + 2] = (byte)red;
                pixels[pixel + 3] = byte.MaxValue;
            }
        }

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

    private static int Clamp(int value) => global::System.Math.Clamp(value, 0, 255);

    private readonly record struct PlaneData(
        byte[] Bytes,
        int RowStride,
        int PixelStride)
    {
        public int Read(int row, int column)
        {
            var index = row * RowStride + column * PixelStride;
            return index >= 0 && index < Bytes.Length
                ? Bytes[index]
                : 128;
        }
    }
}
