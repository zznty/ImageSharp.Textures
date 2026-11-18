namespace ImageSharp.Textures.Formats.Ktx2;

/// <summary>
///     Describes the Khronos Texture 2 (KTX2) container format.
/// </summary>
public sealed class Ktx2Format : ITextureFormat<Ktx2TextureMetadata>
{
    private static readonly string[] MimeTypeValues = ["image/ktx2"];
    private static readonly string[] FileExtensionValues = [".ktx2"];

    private Ktx2Format()
    {
    }

    /// <summary>
    ///     Gets the shared KTX2 format descriptor.
    /// </summary>
    public static Ktx2Format Instance { get; } = new();

    /// <inheritdoc />
    public string Name => "KTX2";

    /// <inheritdoc />
    public string DefaultMimeType => "image/ktx2";

    /// <inheritdoc />
    public IEnumerable<string> MimeTypes => MimeTypeValues;

    /// <inheritdoc />
    public IEnumerable<string> FileExtensions => FileExtensionValues;

    /// <inheritdoc />
    public Ktx2TextureMetadata CreateDefaultFormatMetadata() => new();
}
