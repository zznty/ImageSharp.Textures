namespace ImageSharp.Textures;

/// <summary>
///     Decodes texture containers into <see cref="Texture" /> instances.
/// </summary>
public interface ITextureDecoder
{
    /// <summary>
    ///     Gets the format the decoder handles.
    /// </summary>
    public ITextureFormat Format { get; }

    /// <summary>
    ///     Decodes a texture from the given stream.
    /// </summary>
    /// <param name="configuration">The texture configuration to use.</param>
    /// <param name="stream">The stream containing the encoded texture.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The decoded texture.</returns>
    public Texture Decode(TextureConfiguration configuration, Stream stream,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Asynchronously decodes a texture from the given stream.
    /// </summary>
    /// <param name="configuration">The texture configuration to use.</param>
    /// <param name="stream">The stream containing the encoded texture.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The decoded texture.</returns>
    public Task<Texture> DecodeAsync(TextureConfiguration configuration, Stream stream,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Identifies the texture in the given stream without decoding its surfaces.
    /// </summary>
    /// <param name="configuration">The texture configuration to use.</param>
    /// <param name="stream">The stream containing the encoded texture.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The texture information.</returns>
    public TextureInfo Identify(TextureConfiguration configuration, Stream stream,
        CancellationToken cancellationToken = default);
}
