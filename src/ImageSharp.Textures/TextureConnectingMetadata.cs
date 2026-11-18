namespace ImageSharp.Textures;

/// <summary>
///     Stores metadata used to convert between texture formats.
/// </summary>
public sealed class TextureConnectingMetadata
{
    /// <summary>
    ///     Gets information about the encoded texel type.
    /// </summary>
    public TexelTypeInfo TexelTypeInfo { get; init; }
}
