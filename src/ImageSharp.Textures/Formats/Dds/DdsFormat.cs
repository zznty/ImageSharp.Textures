namespace ImageSharp.Textures.Formats.Dds;

/// <summary>
///     Describes the DirectDraw Surface (DDS) texture container format.
/// </summary>
public sealed class DdsFormat : ITextureFormat<DdsTextureMetadata>
{
    private static readonly string[] MimeTypeValues = ["image/vnd.ms-dds"];
    private static readonly string[] FileExtensionValues = [".dds"];

    private DdsFormat()
    {
    }

    /// <summary>
    ///     Gets the shared DDS format descriptor.
    /// </summary>
    public static DdsFormat Instance { get; } = new();

    /// <inheritdoc />
    public string Name => "DDS";

    /// <inheritdoc />
    public string DefaultMimeType => "image/vnd.ms-dds";

    /// <inheritdoc />
    public IEnumerable<string> MimeTypes => MimeTypeValues;

    /// <inheritdoc />
    public IEnumerable<string> FileExtensions => FileExtensionValues;

    /// <inheritdoc />
    public DdsTextureMetadata CreateDefaultFormatMetadata() => new();
}
