using ImageSharp.Textures.Texels.BcCommon;

namespace ImageSharp.Textures.Tests.TexelOperations;

/// <summary>
///     Shared helpers for the block compression encode tests.
/// </summary>
internal static class BcEncodeTestHelpers
{
    internal static Image<Rgba32> CreateImage(int width, int height, Func<int, int, Rgba32> pixel)
    {
        Image<Rgba32> image = new(width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                image[x, y] = pixel(x, y);
            }
        }

        return image;
    }

    internal static byte[] Encode(TexelFormat format, Image<Rgba32> image, BcEncodeOptions? options = null)
    {
        Textures.TexelOperations operations = TexelFormatRegistry.GetOperations(format);
        byte[] payload = new byte[operations.GetEncodedByteCount(image.Width, image.Height)];
        BcEncode.Encode(operations, image, payload, options ?? BcEncodeOptions.Default);
        return payload;
    }

    internal static Image<Rgba32> Decode(TexelFormat format, byte[] payload, int width, int height)
        => TexelFormatRegistry.GetOperations(format).Decode<Rgba32>(payload, Configuration.Default, width, height);

    internal static int GetEncodedByteCount(TexelFormat format, int width, int height)
        => TexelFormatRegistry.GetOperations(format).GetEncodedByteCount(width, height);

    internal static void AssertPayload(string expectedHex, byte[] actual, string description)
    {
        string actualHex = Convert.ToHexString(actual);
        Assert.True(actualHex == expectedHex, $"{description}: expected {expectedHex} but encoded {actualHex}.");
    }

    /// <summary>
    ///     Asserts that no compared channel differs by more than <paramref name="maxError" />, and reports the
    ///     worst channel, the offending texel and the format/dimensions on failure.
    /// </summary>
    internal static void AssertMaxChannelError(
        Image<Rgba32> expected,
        Image<Rgba32> actual,
        int maxError,
        string formatName,
        bool compareGreen = true,
        bool compareBlue = true,
        bool compareAlpha = true)
    {
        int worst = 0;
        int worstX = -1;
        int worstY = -1;

        for (int y = 0; y < expected.Height; y++)
        {
            for (int x = 0; x < expected.Width; x++)
            {
                Rgba32 e = expected[x, y];
                Rgba32 a = actual[x, y];

                int diff = Math.Abs(e.R - a.R);

                if (compareGreen)
                {
                    diff = Math.Max(diff, Math.Abs(e.G - a.G));
                }

                if (compareBlue)
                {
                    diff = Math.Max(diff, Math.Abs(e.B - a.B));
                }

                if (compareAlpha)
                {
                    diff = Math.Max(diff, Math.Abs(e.A - a.A));
                }

                if (diff > worst)
                {
                    worst = diff;
                    worstX = x;
                    worstY = y;
                }
            }
        }

        string message = $"{formatName} {expected.Width}x{expected.Height} encode/decode differs by {worst} at "
                         + $"({worstX},{worstY}); the limit is {maxError} LSB.";

        Assert.True(worst <= maxError, message);
    }
}
