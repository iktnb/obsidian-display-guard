using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ObsidianDisplayGuard;

internal static class BlurredScreenCapture
{
    private const int DownsampleFactor = 4;
    private const int BlurRadius = 9;
    private const double BlurSigma = 5.5;

    internal static Bitmap Capture(Rectangle bounds)
    {
        using var screenshot = new Bitmap(
            bounds.Width,
            bounds.Height,
            PixelFormat.Format32bppPArgb);

        using (var graphics = Graphics.FromImage(screenshot))
        {
            graphics.CopyFromScreen(
                bounds.Location,
                Point.Empty,
                bounds.Size,
                CopyPixelOperation.SourceCopy);
        }

        var reducedSize = new Size(
            Math.Max(1, bounds.Width / DownsampleFactor),
            Math.Max(1, bounds.Height / DownsampleFactor));

        using var reduced = Resize(screenshot, reducedSize);
        using var blurred = ApplyGaussianBlur(reduced, BlurRadius, BlurSigma);
        return Resize(blurred, bounds.Size);
    }

    private static Bitmap Resize(Bitmap source, Size size)
    {
        var resized = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(resized);
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.DrawImage(source, new Rectangle(Point.Empty, size));
        return resized;
    }

    private static Bitmap ApplyGaussianBlur(Bitmap source, int radius, double sigma)
    {
        var rectangle = new Rectangle(Point.Empty, source.Size);
        var sourceData = source.LockBits(
            rectangle,
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppPArgb);

        try
        {
            var byteCount = Math.Abs(sourceData.Stride) * source.Height;
            var input = new byte[byteCount];
            var horizontal = new byte[byteCount];
            var output = new byte[byteCount];
            Marshal.Copy(sourceData.Scan0, input, 0, byteCount);

            var kernel = CreateKernel(radius, sigma);
            BlurHorizontal(input, horizontal, source.Width, source.Height, sourceData.Stride, kernel, radius);
            BlurVertical(horizontal, output, source.Width, source.Height, sourceData.Stride, kernel, radius);

            var result = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppPArgb);
            var resultData = result.LockBits(
                rectangle,
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppPArgb);
            try
            {
                Marshal.Copy(output, 0, resultData.Scan0, byteCount);
            }
            finally
            {
                result.UnlockBits(resultData);
            }

            return result;
        }
        finally
        {
            source.UnlockBits(sourceData);
        }
    }

    private static double[] CreateKernel(int radius, double sigma)
    {
        var kernel = new double[(radius * 2) + 1];
        var sum = 0D;
        for (var offset = -radius; offset <= radius; offset++)
        {
            var value = Math.Exp(-(offset * offset) / (2 * sigma * sigma));
            kernel[offset + radius] = value;
            sum += value;
        }

        for (var index = 0; index < kernel.Length; index++)
        {
            kernel[index] /= sum;
        }

        return kernel;
    }

    private static void BlurHorizontal(
        byte[] input,
        byte[] output,
        int width,
        int height,
        int stride,
        double[] kernel,
        int radius)
    {
        Parallel.For(0, height, y =>
        {
            for (var x = 0; x < width; x++)
            {
                var outputIndex = (y * stride) + (x * 4);
                for (var channel = 0; channel < 3; channel++)
                {
                    var sum = 0D;
                    for (var offset = -radius; offset <= radius; offset++)
                    {
                        var sampleX = Math.Clamp(x + offset, 0, width - 1);
                        sum += input[(y * stride) + (sampleX * 4) + channel] *
                               kernel[offset + radius];
                    }

                    output[outputIndex + channel] = (byte)Math.Clamp((int)Math.Round(sum), 0, 255);
                }

                output[outputIndex + 3] = 255;
            }
        });
    }

    private static void BlurVertical(
        byte[] input,
        byte[] output,
        int width,
        int height,
        int stride,
        double[] kernel,
        int radius)
    {
        Parallel.For(0, height, y =>
        {
            for (var x = 0; x < width; x++)
            {
                var outputIndex = (y * stride) + (x * 4);
                for (var channel = 0; channel < 3; channel++)
                {
                    var sum = 0D;
                    for (var offset = -radius; offset <= radius; offset++)
                    {
                        var sampleY = Math.Clamp(y + offset, 0, height - 1);
                        sum += input[(sampleY * stride) + (x * 4) + channel] *
                               kernel[offset + radius];
                    }

                    output[outputIndex + channel] = (byte)Math.Clamp((int)Math.Round(sum), 0, 255);
                }

                output[outputIndex + 3] = 255;
            }
        });
    }
}
