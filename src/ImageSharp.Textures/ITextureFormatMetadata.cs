namespace ImageSharp.Textures;

/// <summary>
///     Provides metadata for a specific texture format.
/// </summary>
public interface ITextureFormatMetadata : IDeepCloneable
{
    /// <summary>
    ///     Converts the metadata to a <see cref="TextureConnectingMetadata" /> instance.
    /// </summary>
    /// <returns>The connecting texture metadata.</returns>
    public TextureConnectingMetadata ToTextureConnectingMetadata();
}
