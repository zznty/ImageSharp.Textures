using ImageSharp.Textures.Formats.Dds;
using ImageSharp.Textures.Formats.Ktx2;
using ImageSharp.Textures.Formats.Ktx;

namespace ImageSharp.Textures;

/// <summary>
///     Provides the memory, pixel operations and texture container formats used by the texture model.
/// </summary>
public sealed class TextureConfiguration
{
    private static readonly Lazy<TextureConfiguration> LazyDefault = new(CreateDefaultInstance);

    /// <summary>
    ///     Initializes a new instance of the <see cref="TextureConfiguration" /> class.
    /// </summary>
    /// <param name="imageSharpConfiguration">The ImageSharp configuration that provides memory and pixel operations.</param>
    public TextureConfiguration(Configuration imageSharpConfiguration)
    {
        ArgumentNullException.ThrowIfNull(imageSharpConfiguration);
        this.ImageSharpConfiguration = imageSharpConfiguration;
        this.Formats = new TextureFormatManager();
    }

    /// <summary>
    ///     Gets the default texture configuration.
    /// </summary>
    public static TextureConfiguration Default => LazyDefault.Value;

    /// <summary>
    ///     Gets the ImageSharp configuration that provides memory and pixel operations.
    /// </summary>
    public Configuration ImageSharpConfiguration { get; }

    /// <summary>
    ///     Gets the texture format manager.
    /// </summary>
    public TextureFormatManager Formats { get; }

    private static TextureConfiguration CreateDefaultInstance()
    {
        TextureConfiguration configuration = new(Configuration.Default);

        configuration.Formats.AddFormat(
            DdsFormat.Instance,
            new DdsTextureDecoder(),
            new DdsTextureEncoder(),
            new DdsFormatDetector());
        configuration.Formats.AddFormat(
            KtxFormat.Instance,
            new KtxTextureDecoder(),
            new KtxTextureEncoder(),
            new KtxFormatDetector());
        configuration.Formats.AddFormat(
            Ktx2Format.Instance,
            new Ktx2TextureDecoder(),
            new Ktx2TextureEncoder(),
            new Ktx2FormatDetector());

        return configuration;
    }
}
