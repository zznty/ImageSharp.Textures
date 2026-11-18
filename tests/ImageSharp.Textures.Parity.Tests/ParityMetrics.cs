using System.Globalization;
using ImageSharp.Textures.Parity.Tests.TestUtilities;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace ImageSharp.Textures.Parity.Tests;

/// <summary>
///     Per-channel diff metrics for the decode parity suite.
/// </summary>
internal static class ParityMetrics
{
    /// <summary>
    ///     Compares a decoded surface against the texconv reference dump.
    /// </summary>
    /// <param name="expected">The reference RGBA32 pixels.</param>
    /// <param name="actual">The decoded RGBA32 pixels.</param>
    /// <param name="width">The surface width.</param>
    /// <param name="height">The surface height.</param>
    /// <param name="channels">The channels to compare.</param>
    /// <returns>The metrics.</returns>
    internal static ParityDiff Compare(
        ReadOnlySpan<byte> expected,
        ReadOnlySpan<byte> actual,
        int width,
        int height,
        ParityChannels channels)
    {
        int pixels = width * height;
        int maxDiff = 0;
        int differing = 0;
        long colourError = 0;
        long alphaError = 0;
        int colourSamples = 0;
        int alphaSamples = 0;

        for (int i = 0; i < pixels; i++)
        {
            int offset = i * 4;
            bool differs = false;

            for (int channel = 0; channel < 4; channel++)
            {
                ParityChannels flag = channel switch
                {
                    0 => ParityChannels.Red,
                    1 => ParityChannels.Green,
                    2 => ParityChannels.Blue,
                    _ => ParityChannels.Alpha
                };

                if ((channels & flag) == 0)
                {
                    continue;
                }

                int diff = Math.Abs(expected[offset + channel] - actual[offset + channel]);
                maxDiff = Math.Max(maxDiff, diff);

                if (diff != 0)
                {
                    differs = true;
                }

                if (flag == ParityChannels.Alpha)
                {
                    alphaError += (long)diff * diff;
                    alphaSamples++;
                }
                else
                {
                    colourError += (long)diff * diff;
                    colourSamples++;
                }
            }

            if (differs)
            {
                differing++;
            }
        }

        double rgbPsnr = colourSamples == 0 ? double.NaN : Psnr(colourError, colourSamples);
        double alphaPsnr = alphaSamples == 0 ? double.NaN : Psnr(alphaError, alphaSamples);

        return new ParityDiff(maxDiff, differing, rgbPsnr, alphaPsnr);
    }

    /// <summary>
    ///     Asserts the decode parity gate for one format and corpus image, writing a diff image on failure.
    /// </summary>
    /// <param name="format">The texconv format spelling.</param>
    /// <param name="image">The corpus image.</param>
    /// <param name="expected">The texconv reference pixels.</param>
    /// <param name="actual">Our decoded pixels.</param>
    /// <param name="channels">The channels to compare.</param>
    /// <param name="maxAllowedDiff">The largest permitted per-channel difference.</param>
    /// <param name="workspace">The workspace used to save the diff image.</param>
    /// <param name="output">The test output.</param>
    /// <returns>The metrics.</returns>
    internal static ParityDiff AssertDecodeParity(
        string format,
        ParityImage image,
        ReadOnlySpan<byte> expected,
        ReadOnlySpan<byte> actual,
        ParityChannels channels,
        int maxAllowedDiff,
        ParityWorkspace workspace,
        ITestOutputHelper output)
    {
        ParityDiff diff = Compare(expected, actual, image.Width, image.Height, channels);
        string summary =
            $"{format,-10} {image.Name,-20} {image.Width,4}x{image.Height,-4} max {diff.MaxChannelDiff,3}  " +
            $"differing {diff.DifferingPixels,7}  rgb {FormatPsnr(diff.RgbPsnr),7}  alpha {FormatPsnr(diff.AlphaPsnr),7}";

        output.WriteLine(summary);

        if (diff.MaxChannelDiff > maxAllowedDiff)
        {
            string diffPath = workspace.PathFor("diff.png");
            SaveDiffImage(expected, actual, image.Width, image.Height, diffPath);

            throw new XunitException(
                $"{format} decode parity failed for '{image.Name}' ({image.Width}x{image.Height}, channels {channels}).{Environment.NewLine}" +
                $"max channel diff {diff.MaxChannelDiff} exceeds {maxAllowedDiff}; {diff.DifferingPixels} of {image.Width * image.Height} pixels differ;" +
                $" RGB PSNR {FormatPsnr(diff.RgbPsnr)}, alpha PSNR {FormatPsnr(diff.AlphaPsnr)}.{Environment.NewLine}" +
                $"Reference and diff image kept in {workspace.DirectoryPath}.");
        }

        return diff;
    }

    /// <summary>
    ///     Formats a PSNR value for reporting.
    /// </summary>
    /// <param name="psnr">The value, possibly NaN.</param>
    /// <returns>The formatted value.</returns>
    internal static string FormatPsnr(double psnr)
        => double.IsNaN(psnr) ? "n/a" :
            double.IsPositiveInfinity(psnr) ? "inf" : psnr.ToString("0.00", CultureInfo.InvariantCulture);

    private static double Psnr(long squaredError, int samples)
        => squaredError == 0 ? double.PositiveInfinity : 10 * Math.Log10(255.0 * 255.0 * samples / squaredError);

    private static void SaveDiffImage(ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual, int width, int height,
        string path)
    {
        using Image<Rgba32> image = new(width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int offset = ((y * width) + x) * 4;
                bool differs = !expected.Slice(offset, 4).SequenceEqual(actual.Slice(offset, 4));
                image[x, y] = differs
                    ? new Rgba32(255, 0, 255, 255)
                    : new Rgba32(expected[offset], expected[offset + 1], expected[offset + 2], 255);
            }
        }

        image.SaveAsPng(path);
    }
}
