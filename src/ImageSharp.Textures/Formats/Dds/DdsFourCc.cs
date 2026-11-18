namespace ImageSharp.Textures.Formats.Dds;

/// <summary>
///     Identifies a legacy four character code stored in <c>DDS_PIXELFORMAT.dwFourCC</c>.
/// </summary>
/// <remarks>
///     Only the codes this library supports are declared, plus <see cref="DX10" /> which announces the DX10
///     extension header. Any other code is rejected with a <see cref="System.NotSupportedException" /> naming
///     the four characters.
/// </remarks>
public enum DdsFourCc : uint
{
    /// <summary>
    ///     No four character code is present; the file used a DX10 header or an uncompressed pixel format.
    /// </summary>
    None = 0,

    /// <summary>
    ///     BC1 block compression (<c>DXT1</c>).
    /// </summary>
    DXT1 = 'D' | ('X' << 8) | ('T' << 16) | ('1' << 24),

    /// <summary>
    ///     The DX10 extension header follows <c>DDS_HEADER</c>.
    /// </summary>
    DX10 = 'D' | ('X' << 8) | ('1' << 16) | ('0' << 24),

    /// <summary>
    ///     BC4 single channel block compression, unsigned normalized (<c>BC4U</c>).
    /// </summary>
    BC4U = 'B' | ('C' << 8) | ('4' << 16) | ('U' << 24),

    /// <summary>
    ///     BC5 dual channel block compression, unsigned normalized (<c>BC5U</c>).
    /// </summary>
    BC5U = 'B' | ('C' << 8) | ('5' << 16) | ('U' << 24)
}
