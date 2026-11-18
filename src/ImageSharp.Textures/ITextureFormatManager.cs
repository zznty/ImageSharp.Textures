namespace ImageSharp.Textures;

/// <summary>
///     Provides read-only access to the texture formats known to a <see cref="TextureConfiguration" />.
/// </summary>
public interface ITextureFormatManager
{
    /// <summary>
    ///     Gets the registered texture formats in registration order.
    /// </summary>
    public IEnumerable<ITextureFormat> Formats { get; }

    /// <summary>
    ///     Gets the decoder registered for the given format.
    /// </summary>
    /// <param name="format">The texture format.</param>
    /// <returns>The decoder.</returns>
    /// <exception cref="NotSupportedException">No decoder is registered for the format.</exception>
    public ITextureDecoder GetDecoder(ITextureFormat format);

    /// <summary>
    ///     Tries to get the encoder registered for the given format.
    /// </summary>
    /// <param name="format">The texture format.</param>
    /// <param name="encoder">The encoder, when one is registered.</param>
    /// <returns><see langword="true" /> when an encoder is registered; otherwise, <see langword="false" />.</returns>
    public bool TryGetEncoder(ITextureFormat format, out ITextureEncoder? encoder);

    /// <summary>
    ///     Finds the format registered for the given file extension.
    /// </summary>
    /// <param name="extension">The file extension, with or without a leading period.</param>
    /// <returns>The matching format; otherwise, <see langword="null" />.</returns>
    public ITextureFormat? FindFormatByFileExtension(string extension);

    /// <summary>
    ///     Finds the format registered for the given MIME type.
    /// </summary>
    /// <param name="mimeType">The MIME type.</param>
    /// <returns>The matching format; otherwise, <see langword="null" />.</returns>
    public ITextureFormat? FindFormatByMimeType(string mimeType);

    /// <summary>
    ///     Detects the format of an encoded texture from its leading header bytes.
    /// </summary>
    /// <param name="header">The leading bytes of the encoded texture.</param>
    /// <returns>The detected format; otherwise, <see langword="null" />.</returns>
    public ITextureFormat? DetectFormat(ReadOnlySpan<byte> header);
}
