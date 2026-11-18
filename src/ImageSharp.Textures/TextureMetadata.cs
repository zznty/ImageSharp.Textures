namespace ImageSharp.Textures;

/// <summary>
///     Stores the texture level metadata, including the format specific metadata for every container.
/// </summary>
public sealed class TextureMetadata : IDeepCloneable<TextureMetadata>
{
    private readonly Dictionary<Type, ITextureFormatMetadata> formatMetadata = [];

    /// <summary>
    ///     Initializes a new instance of the <see cref="TextureMetadata" /> class.
    /// </summary>
    public TextureMetadata()
    {
    }

    /// <summary>
    ///     Gets the format of the container the texture was decoded from.
    /// </summary>
    public ITextureFormat? DecodedTextureFormat { get; internal set; }

    /// <summary>
    ///     Gets the metadata stored for every container, in insertion order. Container-neutral consumers use
    ///     this to read layouts contributed by a container other than their own.
    /// </summary>
    internal IReadOnlyCollection<ITextureFormatMetadata> FormatMetadataEntries => this.formatMetadata.Values;

    /// <summary>
    ///     Creates a deep clone of the texture metadata.
    /// </summary>
    /// <returns>The cloned texture metadata.</returns>
    public TextureMetadata DeepClone()
    {
        TextureMetadata clone = new() { DecodedTextureFormat = this.DecodedTextureFormat };

        foreach (KeyValuePair<Type, ITextureFormatMetadata> pair in this.formatMetadata)
        {
            clone.formatMetadata[pair.Key] = (ITextureFormatMetadata)pair.Value.DeepClone();
        }

        return clone;
    }

    /// <summary>
    ///     Gets the metadata for the given texture format, creating the format defaults when it has not been set.
    /// </summary>
    /// <typeparam name="TFormatMetadata">The type of the format metadata.</typeparam>
    /// <param name="key">The texture format.</param>
    /// <returns>The format metadata.</returns>
    public TFormatMetadata GetFormatMetadata<TFormatMetadata>(ITextureFormat<TFormatMetadata> key)
        where TFormatMetadata : class, ITextureFormatMetadata<TFormatMetadata>
    {
        ArgumentNullException.ThrowIfNull(key);

        Type type = key.GetType();

        if (!this.formatMetadata.TryGetValue(type, out ITextureFormatMetadata? metadata))
        {
            metadata = key.CreateDefaultFormatMetadata();
            this.formatMetadata[type] = metadata;
        }

        return metadata as TFormatMetadata
               ?? throw new InvalidOperationException(
                   $"The metadata registered for the '{key.Name}' texture format is not a {typeof(TFormatMetadata).Name}.");
    }

    /// <summary>
    ///     Creates a deep clone of the metadata for the given texture format.
    /// </summary>
    /// <typeparam name="TFormatMetadata">The type of the format metadata.</typeparam>
    /// <param name="key">The texture format.</param>
    /// <returns>The cloned format metadata.</returns>
    public TFormatMetadata CloneFormatMetadata<TFormatMetadata>(ITextureFormat<TFormatMetadata> key)
        where TFormatMetadata : class, ITextureFormatMetadata<TFormatMetadata>
        => ((IDeepCloneable<TFormatMetadata>)this.GetFormatMetadata(key)).DeepClone();

    /// <summary>
    ///     Tries to get the metadata already stored for the given texture format without creating the format
    ///     defaults, so that a save never adds metadata to the texture it is reading.
    /// </summary>
    /// <typeparam name="TFormatMetadata">The type of the format metadata.</typeparam>
    /// <param name="key">The texture format.</param>
    /// <param name="metadata">The stored metadata, when present.</param>
    /// <returns><see langword="true" /> when metadata of the requested type is stored; otherwise, <see langword="false" />.</returns>
    internal bool TryGetFormatMetadata<TFormatMetadata>(ITextureFormat<TFormatMetadata> key,
        out TFormatMetadata? metadata)
        where TFormatMetadata : class, ITextureFormatMetadata<TFormatMetadata>
    {
        ArgumentNullException.ThrowIfNull(key);

        if (this.formatMetadata.TryGetValue(key.GetType(), out ITextureFormatMetadata? stored)
            && stored is TFormatMetadata typed)
        {
            metadata = typed;
            return true;
        }

        metadata = null;
        return false;
    }
}
