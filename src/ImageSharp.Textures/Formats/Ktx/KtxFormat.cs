namespace ImageSharp.Textures.Formats.Ktx;

/// <summary>
///     Describes the Khronos Texture (KTX v1) container format.
/// </summary>
public sealed class KtxFormat : ITextureFormat<KtxTextureMetadata>
{
    private static readonly string[] MimeTypeValues = ["image/ktx"];
    private static readonly string[] FileExtensionValues = [".ktx"];

    private KtxFormat()
    {
    }

    /// <summary>
    ///     Gets the shared KTX format descriptor.
    /// </summary>
    public static KtxFormat Instance { get; } = new();

    /// <inheritdoc />
    public string Name => "KTX";

    /// <inheritdoc />
    public string DefaultMimeType => "image/ktx";

    /// <inheritdoc />
    public IEnumerable<string> MimeTypes => MimeTypeValues;

    /// <inheritdoc />
    public IEnumerable<string> FileExtensions => FileExtensionValues;

    /// <inheritdoc />
    public KtxTextureMetadata CreateDefaultFormatMetadata() => new();
}
