namespace ImageSharp.Textures.Tests.Texels;

/// <summary>
///     Shared assertions for the block-compressed decode tests.
/// </summary>
internal static class BcDecodeTestHelpers
{
    /// <summary>
    ///     Decodes one block as a 4x4 surface and asserts the RGBA bytes.
    /// </summary>
    /// <param name="operations">The texel operations under test.</param>
    /// <param name="blockHex">The encoded block, as hex.</param>
    /// <param name="expectedHex">The expected RGBA32 bytes of the 4x4 block, as hex.</param>
    internal static void AssertBlock(
        Textures.TexelOperations operations,
        string blockHex,
        string expectedHex)
    {
        byte[] block = Convert.FromHexString(blockHex);
        byte[] expected = Convert.FromHexString(expectedHex);

        using Image<Rgba32> image = operations.Decode<Rgba32>(block, Configuration.Default, 4, 4);
        Assert.Equal(expected, CopyPixels(image));
    }

    /// <summary>
    ///     Builds a payload by repeating one block across the block grid of a surface.
    /// </summary>
    /// <param name="width">The surface width, in texels.</param>
    /// <param name="height">The surface height, in texels.</param>
    /// <param name="block">The block to repeat.</param>
    /// <returns>The encoded payload.</returns>
    internal static byte[] CreatePayload(int width, int height, ReadOnlySpan<byte> block)
    {
        int blocks = (width + 3) / 4 * ((height + 3) / 4);
        byte[] payload = new byte[blocks * block.Length];

        for (int i = 0; i < blocks; i++)
        {
            block.CopyTo(payload.AsSpan(i * block.Length));
        }

        return payload;
    }

    /// <summary>
    ///     Asserts that every texel of a surface decodes to the corresponding texel of the repeated block,
    ///     which also proves that partial edge blocks are clipped to the visible rows and columns.
    /// </summary>
    /// <param name="image">The decoded surface.</param>
    /// <param name="blockRgba">The expected 4x4 RGBA32 bytes of one block.</param>
    internal static void AssertRepeatsBlock(Image<Rgba32> image, ReadOnlySpan<byte> blockRgba)
    {
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                int offset = (((y & 3) * 4) + (x & 3)) * 4;
                Rgba32 expected = new(blockRgba[offset], blockRgba[offset + 1], blockRgba[offset + 2],
                    blockRgba[offset + 3]);
                Assert.Equal(expected, image[x, y]);
            }
        }
    }

    /// <summary>
    ///     Copies the pixels of an image into RGBA32 bytes.
    /// </summary>
    /// <param name="image">The image.</param>
    /// <returns>The packed pixels.</returns>
    internal static byte[] CopyPixels(Image<Rgba32> image)
    {
        byte[] bytes = new byte[image.Width * image.Height * 4];
        image.CopyPixelDataTo(bytes);
        return bytes;
    }
}
