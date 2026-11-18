namespace ImageSharp.Textures.Formats.Dds;

/// <summary>
///     The face layout of a texture, resolved from the container metadata it carries.
/// </summary>
internal readonly struct TextureLayout
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TextureLayout" /> struct.
    /// </summary>
    /// <param name="faceCount">The number of cube map faces.</param>
    /// <param name="source">The metadata type that declared the layout, or <see langword="null" />.</param>
    internal TextureLayout(int faceCount, string? source)
    {
        this.FaceCount = faceCount;
        this.Source = source;
    }

    /// <summary>
    ///     Gets the layout of a texture whose metadata does not describe one: a single face.
    /// </summary>
    internal static TextureLayout Unclaimed { get; } = new(1, null);

    /// <summary>
    ///     Gets the number of cube map faces: 6 for a cube map, 1 otherwise.
    /// </summary>
    internal int FaceCount { get; }

    /// <summary>
    ///     Gets the metadata type that declared the layout, or <see langword="null" /> when no metadata did.
    /// </summary>
    internal string? Source { get; }

    /// <summary>
    ///     Gets a value indicating whether a container metadata object declared the layout.
    /// </summary>
    internal bool HasClaim => this.Source is not null;

    /// <summary>
    ///     Gets a value indicating whether the declared layout is a cube map.
    /// </summary>
    internal bool IsCubeMap => this.FaceCount == 6;
}
