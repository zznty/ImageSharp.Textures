namespace ImageSharp.Textures;

/// <summary>
///     Provides the descriptive information for a texture container format.
/// </summary>
/// <typeparam name="TFormatMetadata">The metadata type used by the format.</typeparam>
public interface ITextureFormat<out TFormatMetadata> : ITextureFormat
    where TFormatMetadata : class
{
    /// <summary>
    ///     Creates the default metadata for the format.
    /// </summary>
    /// <returns>The format metadata.</returns>
    public TFormatMetadata CreateDefaultFormatMetadata();
}
