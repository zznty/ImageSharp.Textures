namespace ImageSharp.Textures.Tests.TestUtilities;

/// <summary>
///     Builds texture graphs for the model and interop tests.
/// </summary>
internal static class TestTextureFactory
{
    /// <summary>
    ///     Creates a texture configuration with its own empty format manager.
    /// </summary>
    /// <returns>The texture configuration.</returns>
    internal static TextureConfiguration CreateConfiguration() => new(Configuration.Default);

    /// <summary>
    ///     Creates a texture with one mip level holding the requested number of surfaces.
    /// </summary>
    /// <param name="configuration">The texture configuration.</param>
    /// <param name="width">The surface width.</param>
    /// <param name="height">The surface height.</param>
    /// <param name="surfaceCount">The number of surfaces in the base mip level.</param>
    /// <param name="texelFormat">The texel format of the surfaces.</param>
    /// <returns>The texture.</returns>
    internal static Texture CreateTexture(
        TextureConfiguration configuration,
        int width,
        int height,
        int surfaceCount = 1,
        TexelFormat texelFormat = TexelFormat.R8G8B8A8_UNorm)
    {
        Texture texture = new(configuration, new TextureMetadata());
        TextureMipLevel mipLevel = texture.AddMipLevel(width, height, 1);

        for (int i = 0; i < surfaceCount; i++)
        {
            TextureSurface surface = mipLevel.CreateSurface(texelFormat);
            FillSurfacePattern(surface, (byte)i);
        }

        return texture;
    }

    /// <summary>
    ///     Fills an R8G8B8A8_UNorm surface with a deterministic pattern derived from the seed.
    /// </summary>
    /// <param name="surface">The surface to fill.</param>
    /// <param name="seed">The pattern seed.</param>
    internal static void FillSurfacePattern(TextureSurface surface, byte seed)
    {
        Span<byte> payload = surface.GetPayload();

        for (int i = 0; i < payload.Length; i += 4)
        {
            int texel = i / 4;
            payload[i] = (byte)((texel + seed) & 0xFF);
            payload[i + 1] = (byte)(((texel * 3) + seed) & 0xFF);
            payload[i + 2] = (byte)(((texel * 7) + seed) & 0xFF);
            payload[i + 3] = (byte)(255 - ((texel + seed) & 0xFF));
        }
    }
}
