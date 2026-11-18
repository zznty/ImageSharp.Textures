namespace ImageSharp.Textures;

/// <summary>
///     Represents the transparency behavior of a texel type.
/// </summary>
public enum TexelAlphaRepresentation
{
    /// <summary>
    ///     The texel type does not contain alpha.
    /// </summary>
    None,

    /// <summary>
    ///     Color components are stored after being multiplied by alpha.
    /// </summary>
    Associated,

    /// <summary>
    ///     Color components are stored independently from alpha.
    /// </summary>
    Unassociated,

    /// <summary>
    ///     Alpha behavior is not specified by the texture metadata.
    /// </summary>
    Unknown
}
