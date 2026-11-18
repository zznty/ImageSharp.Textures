namespace ImageSharp.Textures;

/// <summary>
///     Represents the surfaces stored at one mip level of a texture.
/// </summary>
public sealed class TextureMipLevel : IDisposable
{
    private bool isDisposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TextureMipLevel" /> class.
    /// </summary>
    /// <param name="configuration">The texture configuration used to allocate surface payloads.</param>
    /// <param name="width">The width of surfaces at this mip level.</param>
    /// <param name="height">The height of surfaces at this mip level.</param>
    /// <param name="depth">The depth of this mip level; 1 for flat, array and cubemap textures.</param>
    internal TextureMipLevel(TextureConfiguration configuration, int width, int height, int depth)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "The mip level width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "The mip level height must be positive.");
        }

        if (depth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(depth), depth, "The mip level depth must be positive.");
        }

        this.Configuration = configuration;
        this.Width = width;
        this.Height = height;
        this.Depth = depth;
        this.Surfaces = new TextureSurfaceCollection(this);
    }

    /// <summary>
    ///     Gets the width of surfaces at this mip level.
    /// </summary>
    public int Width { get; }

    /// <summary>
    ///     Gets the height of surfaces at this mip level.
    /// </summary>
    public int Height { get; }

    /// <summary>
    ///     Gets the depth of this mip level; 1 for flat, array and cubemap textures.
    /// </summary>
    public int Depth { get; }

    /// <summary>
    ///     Gets the ordered two-dimensional surfaces stored at this mip level.
    /// </summary>
    public TextureSurfaceCollection Surfaces { get; }

    /// <summary>
    ///     Gets the texture configuration used to allocate surface payloads.
    /// </summary>
    internal TextureConfiguration Configuration { get; }

    /// <summary>
    ///     Releases resources owned by the mip level.
    /// </summary>
    public void Dispose()
    {
        if (this.isDisposed)
        {
            return;
        }

        this.Surfaces.Dispose();
        this.isDisposed = true;
    }

    /// <summary>
    ///     Creates a surface matching this mip level's dimensions and appends it to the surface collection.
    ///     Container decoders call this to materialize one surface per layer, face or depth slice.
    /// </summary>
    /// <param name="texelFormat">The texel format of the surface data.</param>
    /// <returns>The appended surface, whose payload the caller must fill.</returns>
    internal TextureSurface CreateSurface(TexelFormat texelFormat)
    {
        ObjectDisposedException.ThrowIf(this.isDisposed, this);
        TextureSurface surface = new(this.Configuration, texelFormat, this.Width, this.Height);
        this.Surfaces.AppendSurface(surface);
        return surface;
    }

    /// <summary>
    ///     Creates a deep copy of the mip level, including the payloads of every surface.
    /// </summary>
    /// <returns>The cloned mip level.</returns>
    internal TextureMipLevel Clone()
    {
        TextureMipLevel clone = new(this.Configuration, this.Width, this.Height, this.Depth);

        foreach (TextureSurface surface in this.Surfaces)
        {
            clone.Surfaces.AppendSurface(surface.Clone());
        }

        return clone;
    }
}
