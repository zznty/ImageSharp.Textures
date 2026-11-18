namespace ImageSharp.Textures;

/// <summary>
///     Provides metadata for a specific texture format.
/// </summary>
/// <typeparam name="TSelf">The metadata type implementing this interface.</typeparam>
public interface ITextureFormatMetadata<TSelf> : ITextureFormatMetadata, IDeepCloneable<TSelf>
    where TSelf : class, ITextureFormatMetadata
{
    /// <summary>
    ///     Creates a new instance of the <typeparamref name="TSelf" /> class from the given
    ///     <see cref="TextureConnectingMetadata" />.
    /// </summary>
    /// <param name="metadata">The connecting texture metadata.</param>
    /// <returns>The <typeparamref name="TSelf" />.</returns>
#pragma warning disable CA1000 // Do not declare static members on generic types
    public static abstract TSelf FromTextureConnectingMetadata(TextureConnectingMetadata metadata);
#pragma warning restore CA1000 // Do not declare static members on generic types
}
