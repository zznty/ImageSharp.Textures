namespace ImageSharp.Textures;

/// <summary>
///     Describes the layout of a texture without materializing its surfaces.
/// </summary>
public sealed class TextureInfo
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TextureInfo" /> class.
    /// </summary>
    /// <param name="width">The width of the base mip level, in texels.</param>
    /// <param name="height">The height of the base mip level, in texels.</param>
    /// <param name="depth">The depth of the base mip level; 1 for flat, array and cubemap textures.</param>
    /// <param name="mipLevelCount">The number of mip levels.</param>
    /// <param name="surfaceCount">The total number of surfaces across every mip level.</param>
    /// <param name="texelFormat">The texel format of the surfaces.</param>
    public TextureInfo(int width, int height, int depth, int mipLevelCount, int surfaceCount, TexelFormat texelFormat)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "The texture width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "The texture height must be positive.");
        }

        if (depth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(depth), depth, "The texture depth must be positive.");
        }

        if (mipLevelCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(mipLevelCount),
                mipLevelCount,
                "The texture must contain at least one mip level.");
        }

        if (surfaceCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(surfaceCount),
                surfaceCount,
                "The texture must contain at least one surface.");
        }

        this.Width = width;
        this.Height = height;
        this.Depth = depth;
        this.MipLevelCount = mipLevelCount;
        this.SurfaceCount = surfaceCount;
        this.TexelFormat = texelFormat;
    }

    /// <summary>
    ///     Gets the width of the base mip level, in texels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    ///     Gets the height of the base mip level, in texels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    ///     Gets the depth of the base mip level; 1 for flat, array and cubemap textures.
    /// </summary>
    public int Depth { get; }

    /// <summary>
    ///     Gets the number of mip levels.
    /// </summary>
    public int MipLevelCount { get; }

    /// <summary>
    ///     Gets the total number of surfaces across every mip level.
    /// </summary>
    public int SurfaceCount { get; }

    /// <summary>
    ///     Gets the texel format of the surfaces.
    /// </summary>
    public TexelFormat TexelFormat { get; }
}
