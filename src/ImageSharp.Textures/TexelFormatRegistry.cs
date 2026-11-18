using ImageSharp.Textures.Texels.Bc1;
using ImageSharp.Textures.Texels.Bc4;
using ImageSharp.Textures.Texels.Bc5;
using ImageSharp.Textures.Texels.Bc7;
using ImageSharp.Textures.Texels.Uncompressed;

namespace ImageSharp.Textures;

/// <summary>
///     Maps <see cref="TexelFormat" /> values to their texel operations.
/// </summary>
internal static class TexelFormatRegistry
{
    /// <summary>
    ///     Gets the texel operations that handle the given texel format.
    /// </summary>
    /// <param name="format">The texel format.</param>
    /// <returns>The stateless texel operations instance for the format.</returns>
    /// <exception cref="NotSupportedException">No texel operations are registered for the format.</exception>
    internal static TexelOperations GetOperations(TexelFormat format)
        => format switch
        {
            TexelFormat.R8G8B8A8_UNorm => Rgba32TexelOperations.Instance,
            TexelFormat.Bc1_UNorm => Bc1TexelOperations.Instance,
            TexelFormat.Bc1_UnormSrgb => Bc1TexelOperations.SrgbInstance,
            TexelFormat.Bc4_UNorm => Bc4TexelOperations.Instance,
            TexelFormat.Bc5_UNorm => Bc5TexelOperations.Instance,
            TexelFormat.Bc7_UNorm => Bc7TexelOperations.Instance,
            TexelFormat.Bc7_UnormSrgb => Bc7TexelOperations.SrgbInstance,
            _ => throw new NotSupportedException($"The texel format '{format}' has no registered texel operations.")
        };
}
