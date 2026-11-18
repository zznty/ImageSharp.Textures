namespace ImageSharp.Textures.Formats.Dds.Enums;

/// <summary>
///     Identifies other, less common options for a DDS DX10 resource.
/// </summary>
[Flags]
public enum D3d10ResourceMiscFlags : uint
{
    /// <summary>
    ///     No options are set.
    /// </summary>
    Unknown = 0,

    /// <summary>
    ///     The resource supports mip generation.
    /// </summary>
    GenerateMips = 0x1,

    /// <summary>
    ///     The resource is shared between devices.
    /// </summary>
    Shared = 0x2,

    /// <summary>
    ///     The two-dimensional texture is a cube map.
    /// </summary>
    TextureCube = 0x4,

    /// <summary>
    ///     The resource is synchronized with a keyed mutex.
    /// </summary>
    SharedKeyedMutex = 0x10,

    /// <summary>
    ///     The resource is compatible with GDI.
    /// </summary>
    GdiCompatible = 0x20
}
