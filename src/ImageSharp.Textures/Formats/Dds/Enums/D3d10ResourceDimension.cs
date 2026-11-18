namespace ImageSharp.Textures.Formats.Dds.Enums;

/// <summary>
///     Identifies the type of resource described by a DDS DX10 header.
/// </summary>
public enum D3d10ResourceDimension : uint
{
    /// <summary>
    ///     The resource type is unknown.
    /// </summary>
    Unknown = 0,

    /// <summary>
    ///     The resource is a buffer.
    /// </summary>
    Buffer = 1,

    /// <summary>
    ///     The resource is a one-dimensional texture.
    /// </summary>
    Texture1D = 2,

    /// <summary>
    ///     The resource is a two-dimensional texture, optionally a cube map.
    /// </summary>
    Texture2D = 3,

    /// <summary>
    ///     The resource is a three-dimensional (volume) texture.
    /// </summary>
    Texture3D = 4
}
