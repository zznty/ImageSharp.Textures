using System.Buffers;

namespace ImageSharp.Textures;

/// <summary>
///     Represents one editable two-dimensional texture surface.
/// </summary>
public sealed class TextureSurface : IDisposable
{
    private readonly TextureConfiguration configuration;
    private readonly IMemoryOwner<byte> memory;

    private bool isDisposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TextureSurface" /> class and rents its payload from the
    ///     configuration's memory allocator. The payload contents are undefined until a decoder or
    ///     <see cref="CopyFrom{TPixel}" /> writes them.
    /// </summary>
    /// <param name="configuration">The texture configuration providing the memory allocator.</param>
    /// <param name="texelFormat">The texel format of the surface data.</param>
    /// <param name="width">The surface width, in texels.</param>
    /// <param name="height">The surface height, in texels.</param>
    internal TextureSurface(TextureConfiguration configuration, TexelFormat texelFormat, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "The surface width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "The surface height must be positive.");
        }

        this.configuration = configuration;
        this.TexelFormat = texelFormat;
        this.Operations = TexelFormatRegistry.GetOperations(texelFormat);
        this.Width = width;
        this.Height = height;
        this.PayloadLength = this.Operations.GetEncodedByteCount(width, height);
        this.memory = configuration.ImageSharpConfiguration.MemoryAllocator.Allocate<byte>(this.PayloadLength);
    }

    /// <summary>
    ///     Gets the surface width, in texels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    ///     Gets the surface height, in texels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    ///     Gets the texel format of the surface data.
    /// </summary>
    internal TexelFormat TexelFormat { get; }

    /// <summary>
    ///     Gets the operations that convert between the surface payload and ImageSharp images.
    /// </summary>
    internal TexelOperations Operations { get; }

    /// <summary>
    ///     Gets the encoded payload length, in bytes.
    /// </summary>
    internal int PayloadLength { get; }

    /// <summary>
    ///     Releases resources owned by the surface.
    /// </summary>
    public void Dispose()
    {
        if (this.isDisposed)
        {
            return;
        }

        this.memory.Dispose();
        this.isDisposed = true;
    }

    /// <summary>
    ///     Converts the surface to an ImageSharp image using the requested pixel type.
    /// </summary>
    /// <typeparam name="TPixel">The ImageSharp pixel type to use for the returned image.</typeparam>
    /// <returns>An ImageSharp image containing a copy of the surface pixels.</returns>
    public Image<TPixel> ToImage<TPixel>()
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ObjectDisposedException.ThrowIf(this.isDisposed, this);
        return this.Operations.Decode<TPixel>(
            this.GetPayload(),
            this.configuration.ImageSharpConfiguration,
            this.Width,
            this.Height);
    }

    /// <summary>
    ///     Replaces the surface pixels with pixels from the specified ImageSharp image.
    /// </summary>
    /// <typeparam name="TPixel">The ImageSharp pixel type used by the source image.</typeparam>
    /// <param name="image">The image containing the replacement pixels.</param>
    /// <exception cref="ArgumentException">The image dimensions do not match the texture surface dimensions.</exception>
    public void CopyFrom<TPixel>(Image<TPixel> image)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(image);
        ObjectDisposedException.ThrowIf(this.isDisposed, this);

        if (image.Width != this.Width || image.Height != this.Height)
        {
            throw new ArgumentException(
                $"The source image dimensions {image.Width}x{image.Height} do not match the surface dimensions {this.Width}x{this.Height}.",
                nameof(image));
        }

        this.Operations.Encode(image, this.GetPayload());
    }

    /// <summary>
    ///     Gets the writable encoded payload of the surface.
    /// </summary>
    /// <returns>The encoded payload span.</returns>
    internal Span<byte> GetPayload()
    {
        ObjectDisposedException.ThrowIf(this.isDisposed, this);
        return this.memory.Memory.Span[..this.PayloadLength];
    }

    /// <summary>
    ///     Creates a deep copy of the surface, including its payload.
    /// </summary>
    /// <returns>The cloned surface.</returns>
    internal TextureSurface Clone()
    {
        TextureSurface clone = new(this.configuration, this.TexelFormat, this.Width, this.Height);
        this.GetPayload().CopyTo(clone.GetPayload());
        return clone;
    }
}
