namespace ImageSharp.Textures;

/// <summary>
///     Encodes <see cref="Texture" /> instances into texture containers.
/// </summary>
public interface ITextureEncoder
{
    /// <summary>
    ///     Gets the format the encoder handles.
    /// </summary>
    public ITextureFormat Format { get; }

    /// <summary>
    ///     Encodes the given texture to the stream.
    /// </summary>
    /// <param name="texture">The texture to encode.</param>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public void Encode(Texture texture, Stream stream, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Asynchronously encodes the given texture to the stream.
    /// </summary>
    /// <param name="texture">The texture to encode.</param>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous encode operation.</returns>
    public Task EncodeAsync(Texture texture, Stream stream, CancellationToken cancellationToken = default);
}
