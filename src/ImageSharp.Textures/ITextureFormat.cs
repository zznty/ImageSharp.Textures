namespace ImageSharp.Textures;

/// <summary>
///     Provides the descriptive information for a texture container format.
/// </summary>
public interface ITextureFormat
{
    /// <summary>
    ///     Gets the name of the format.
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///     Gets the default MIME type of the format.
    /// </summary>
    public string DefaultMimeType { get; }

    /// <summary>
    ///     Gets all MIME types of the format.
    /// </summary>
    public IEnumerable<string> MimeTypes { get; }

    /// <summary>
    ///     Gets the file extensions of the format, including the leading period.
    /// </summary>
    public IEnumerable<string> FileExtensions { get; }
}
