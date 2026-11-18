using ImageSharp.Textures.Formats.Dds;

namespace ImageSharp.Textures.Formats;

/// <summary>
///     Validates and normalizes the layout of a texture for the KTX v1 and KTX2 encoders.
///     Both containers store one texel format, a base level size and a layer/face/depth grid per level,
///     so the encoders share this projection of the texture graph.
/// </summary>
internal sealed class TextureEncodingLayout
{
    private TextureEncodingLayout(
        TexelFormat texelFormat,
        TexelOperations operations,
        int width,
        int height,
        int baseDepth,
        int layerCount,
        int faceCount,
        int mipLevelCount)
    {
        this.TexelFormat = texelFormat;
        this.Operations = operations;
        this.Width = width;
        this.Height = height;
        this.BaseDepth = baseDepth;
        this.LayerCount = layerCount;
        this.FaceCount = faceCount;
        this.MipLevelCount = mipLevelCount;
    }

    /// <summary>
    ///     Gets the texel format written to the container.
    /// </summary>
    internal TexelFormat TexelFormat { get; }

    /// <summary>
    ///     Gets the texel operations of <see cref="TexelFormat" />.
    /// </summary>
    internal TexelOperations Operations { get; }

    /// <summary>
    ///     Gets the base mip level width.
    /// </summary>
    internal int Width { get; }

    /// <summary>
    ///     Gets the base mip level height.
    /// </summary>
    internal int Height { get; }

    /// <summary>
    ///     Gets the base mip level depth; 1 for flat, array and cubemap textures.
    /// </summary>
    internal int BaseDepth { get; }

    /// <summary>
    ///     Gets the number of array layers.
    /// </summary>
    internal int LayerCount { get; }

    /// <summary>
    ///     Gets the number of cubemap faces; 1 for non-cubemaps.
    /// </summary>
    internal int FaceCount { get; }

    /// <summary>
    ///     Gets the number of mip levels.
    /// </summary>
    internal int MipLevelCount { get; }

    /// <summary>
    ///     Gets a value indicating whether the texture is a cubemap that is not an array cubemap.
    /// </summary>
    internal bool IsNonArrayCubeMap => this.FaceCount == 6 && this.LayerCount == 1;

    /// <summary>
    ///     Gets the width of the given mip level.
    /// </summary>
    /// <param name="level">The mip level index.</param>
    /// <returns>The level width.</returns>
    internal int GetLevelWidth(int level) => Math.Max(1, this.Width >> level);

    /// <summary>
    ///     Gets the height of the given mip level.
    /// </summary>
    /// <param name="level">The mip level index.</param>
    /// <returns>The level height.</returns>
    internal int GetLevelHeight(int level) => Math.Max(1, this.Height >> level);

    /// <summary>
    ///     Gets the depth of the given mip level.
    /// </summary>
    /// <param name="level">The mip level index.</param>
    /// <returns>The level depth.</returns>
    internal int GetLevelDepth(int level) => Math.Max(1, this.BaseDepth >> level);

    /// <summary>
    ///     Creates the encoding layout of a texture.
    /// </summary>
    /// <param name="texture">The texture to encode.</param>
    /// <param name="targetTexelFormat">The texel format to convert to, or <see langword="null" /> to keep the formats.</param>
    /// <param name="layerCount">The array layer count, or <see langword="null" /> to infer it.</param>
    /// <param name="faceCount">
    ///     The cubemap face count, or <see langword="null" /> to resolve it from the container-neutral face layout
    ///     claim in the texture metadata; a texture without a claim has one face.
    /// </param>
    /// <returns>The encoding layout.</returns>
    /// <exception cref="InvalidOperationException">The texture cannot be represented by a single KTX texel format.</exception>
    /// <exception cref="NotSupportedException">The texel format has no registered texel operations.</exception>
    internal static TextureEncodingLayout Create(
        Texture texture,
        TexelFormat? targetTexelFormat,
        int? layerCount,
        int? faceCount)
    {
        ArgumentNullException.ThrowIfNull(texture);

        if (texture.MipLevels.Count == 0)
        {
            throw new InvalidOperationException("The texture does not contain any mip levels.");
        }

        TextureMipLevel baseLevel = texture.MipLevels[0];

        if (baseLevel.Surfaces.Count == 0)
        {
            throw new InvalidOperationException("The base mip level does not contain any surfaces.");
        }

        // Precedence: an explicit encoder option wins, then the container-neutral face layout claim carried by
        // whatever metadata the texture holds (DDS, KTX v1 or KTX2), then a single face.
        int faces = faceCount ?? TextureLayoutResolver.Resolve(texture).FaceCount;
        if (faces is not 1 and not 6)
        {
            throw new InvalidOperationException(
                $"The cubemap face count {faces} is invalid; KTX containers store either 1 or 6 faces.");
        }

        int depth = baseLevel.Depth;

        if (faces == 6 && depth > 1)
        {
            throw new InvalidOperationException(
                $"The texture declares {faces} cube map faces and a depth of {depth}; KTX cubemaps must be "
                + "two dimensional.");
        }

        int surfacesPerLayer = faces * depth;
        if (baseLevel.Surfaces.Count % surfacesPerLayer != 0)
        {
            throw new InvalidOperationException(
                $"The base mip level stores {baseLevel.Surfaces.Count} surfaces, which is not a whole number of "
                + $"{faces} face(s) times {depth} depth slice(s) per array layer.");
        }

        int layers = layerCount ?? baseLevel.Surfaces.Count / surfacesPerLayer;
        if (layers <= 0)
        {
            throw new InvalidOperationException($"The array layer count {layers} must be positive.");
        }

        TexelFormat texelFormat = targetTexelFormat ?? baseLevel.Surfaces[0].TexelFormat;
        if (targetTexelFormat is null)
        {
            foreach (TextureMipLevel level in texture.MipLevels)
            {
                foreach (TextureSurface surface in level.Surfaces)
                {
                    if (surface.TexelFormat != texelFormat)
                    {
                        throw new InvalidOperationException(
                            $"The texture mixes the texel formats '{texelFormat}' and '{surface.TexelFormat}'; a KTX "
                            + "container stores a single texel format. Set KtxEncodingOptions.TargetTexelFormat to "
                            + "convert every surface.");
                    }
                }
            }
        }

        TexelOperations operations = TexelFormatRegistry.GetOperations(texelFormat);

        for (int level = 0; level < texture.MipLevels.Count; level++)
        {
            TextureMipLevel mipLevel = texture.MipLevels[level];
            int expectedWidth = Math.Max(1, baseLevel.Width >> level);
            int expectedHeight = Math.Max(1, baseLevel.Height >> level);
            int expectedDepth = Math.Max(1, depth >> level);

            if (mipLevel.Width != expectedWidth
                || mipLevel.Height != expectedHeight
                || mipLevel.Depth != expectedDepth)
            {
                throw new InvalidOperationException(
                    $"Mip level {level} is {mipLevel.Width}x{mipLevel.Height}x{mipLevel.Depth} but a KTX container "
                    + $"derives {expectedWidth}x{expectedHeight}x{expectedDepth} from the base level.");
            }

            int expectedSurfaces = layers * faces * expectedDepth;
            if (mipLevel.Surfaces.Count != expectedSurfaces)
            {
                throw new InvalidOperationException(
                    $"Mip level {level} stores {mipLevel.Surfaces.Count} surfaces but the {layers} layer(s), "
                    + $"{faces} face(s) and {expectedDepth} depth slice(s) require {expectedSurfaces}.");
            }
        }

        return new TextureEncodingLayout(
            texelFormat,
            operations,
            baseLevel.Width,
            baseLevel.Height,
            depth,
            layers,
            faces,
            texture.MipLevels.Count);
    }
}
