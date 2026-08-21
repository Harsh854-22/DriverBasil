using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace SecureDeviceControl.Dashboard.Imaging;

public static class PerceptualHash
{
    public static ulong ComputeDHash(byte[] imageBytes)
    {
        using var source = new Bitmap(new MemoryStream(imageBytes));
        using var resized = new Bitmap(source, 9, 8);

        var gray = new int[8, 9];
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 9; x++)
            {
                var pixel = resized.GetPixel(x, y);
                gray[y, x] = (pixel.R * 299 + pixel.G * 587 + pixel.B * 114) / 1000;
            }
        }

        ulong hash = 0;
        var bit = 0;
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                if (gray[y, x] > gray[y, x + 1])
                {
                    hash |= 1UL << bit;
                }

                bit++;
            }
        }

        return hash;
    }

    public static int HammingDistance(ulong first, ulong second)
    {
        return System.Numerics.BitOperations.PopCount(first ^ second);
    }
}
