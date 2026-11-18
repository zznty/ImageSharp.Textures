namespace ImageSharp.Textures.Tests.TestUtilities.Ktx;

/// <summary>
///     Builds texture graphs with deterministic per-surface payloads for the KTX container tests.
/// </summary>
internal static class KtxTestTextures
{
    /// <summary>
    ///     Creates a texture whose every surface payload is filled with a single value derived from the surface
    ///     index, so that a decoded texture's surface order can be verified byte by byte.
    /// </summary>
    /// <param name="width">The base level width.</param>
    /// <param name="height">The base level height.</param>
    /// <param name="texelFormat">The texel format of every surface.</param>
    /// <param name="mipLevelCount">The number of mip levels.</param>
    /// <param name="layerCount">The number of array layers.</param>
    /// <param name="faceCount">The number of cubemap faces.</param>
    /// <param name="depth">The base level depth.</param>
    /// <param name="seed">The seed added to every surface index.</param>
    /// <returns>The texture.</returns>
    internal static Texture Create(
        int width,
        int height,
        TexelFormat texelFormat,
        int mipLevelCount = 1,
        int layerCount = 1,
        int faceCount = 1,
        int depth = 1,
        byte seed = 0)
    {
        Texture texture = new(TextureConfiguration.Default, new TextureMetadata());
        int surfaceIndex = 0;

        for (int level = 0; level < mipLevelCount; level++)
        {
            int levelWidth = Math.Max(1, width >> level);
            int levelHeight = Math.Max(1, height >> level);
            int levelDepth = Math.Max(1, depth >> level);
            TextureMipLevel mipLevel = texture.AddMipLevel(levelWidth, levelHeight, levelDepth);

            for (int layer = 0; layer < layerCount; layer++)
            {
                for (int face = 0; face < faceCount; face++)
                {
                    for (int slice = 0; slice < levelDepth; slice++)
                    {
                        TextureSurface surface = mipLevel.CreateSurface(texelFormat);
                        surface.GetPayload().Fill(GetSurfaceValue(seed, surfaceIndex));
                        surfaceIndex++;
                    }
                }
            }
        }

        return texture;
    }

    /// <summary>
    ///     Creates a one level texture whose single surface payload is copied verbatim.
    /// </summary>
    /// <param name="width">The surface width.</param>
    /// <param name="height">The surface height.</param>
    /// <param name="texelFormat">The texel format of the surface.</param>
    /// <param name="payload">The payload bytes, which must match the format's encoded size.</param>
    /// <returns>The texture.</returns>
    internal static Texture CreateSingleSurface(
        int width,
        int height,
        TexelFormat texelFormat,
        params byte[] payload)
    {
        Texture texture = new(TextureConfiguration.Default, new TextureMetadata());
        TextureMipLevel mipLevel = texture.AddMipLevel(width, height, 1);
        TextureSurface surface = mipLevel.CreateSurface(texelFormat);

        if (surface.GetPayload().Length != payload.Length)
        {
            throw new InvalidOperationException(
                $"The '{texelFormat}' surface requires {surface.GetPayload().Length} payload bytes but "
                + $"{payload.Length} were supplied.");
        }

        payload.CopyTo(surface.GetPayload());
        return texture;
    }

    /// <summary>
    ///     Gets the payload byte value that <see cref="Create" /> stores for a surface.
    /// </summary>
    /// <param name="seed">The seed passed to <see cref="Create" />.</param>
    /// <param name="surfaceIndex">The zero-based surface index.</param>
    /// <returns>The payload byte value.</returns>
    internal static byte GetSurfaceValue(byte seed, int surfaceIndex) => (byte)(seed + surfaceIndex);

    /// <summary>
    ///     Asserts that every payload byte of a surface equals the expected value.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="expected">The expected payload byte value.</param>
    internal static void AssertSurfacePayload(TextureSurface surface, byte expected)
        => Assert.All(surface.GetPayload().ToArray(), value => Assert.Equal(expected, value));
}
