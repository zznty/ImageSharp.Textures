namespace ImageSharp.Textures;

/// <summary>
///     Implemented by container metadata that knows how many cube map faces a texture has.
/// </summary>
/// <remarks>
///     Container encoders that can express cube maps need this to preserve a face layout that arrived from
///     another container: a DDS encoder writing a texture loaded from KTX2 sees KTX2 metadata, not DDS metadata,
///     so the face count must be readable through a container-neutral shape. Array layers are not part of this
///     contract: the surface count and the per-level depth already determine them, and letting a container
///     declare a layer count that contradicts the surfaces would turn a layout fact into a guess.
/// </remarks>
internal interface ITextureFaceLayoutMetadata
{
    /// <summary>
    ///     Gets the number of cube map faces the container declares: 6 for a cube map, 1 otherwise.
    /// </summary>
    public int FaceCount { get; }
}
