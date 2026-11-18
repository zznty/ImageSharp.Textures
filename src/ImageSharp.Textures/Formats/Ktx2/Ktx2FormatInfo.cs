using ImageSharp.Textures.Formats.Ktx2.Enums;

namespace ImageSharp.Textures.Formats.Ktx2;

/// <summary>
///     Describes the data format descriptor block that belongs to one Vulkan format.
/// </summary>
internal sealed class Ktx2FormatInfo
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="Ktx2FormatInfo" /> class.
    /// </summary>
    /// <param name="vkFormat">The Vulkan format value.</param>
    /// <param name="texelFormat">The texel format the container stores.</param>
    /// <param name="blockWidth">The texel block width.</param>
    /// <param name="blockHeight">The texel block height.</param>
    /// <param name="blockDepth">The texel block depth.</param>
    /// <param name="blockBytes">The texel block size, in bytes.</param>
    /// <param name="colorModel">The <c>KHR_DF_MODEL</c> value.</param>
    /// <param name="isSrgb">Whether the format stores sRGB encoded values.</param>
    /// <param name="samples">The sample descriptors, in file order.</param>
    internal Ktx2FormatInfo(
        VkFormat vkFormat,
        TexelFormat texelFormat,
        int blockWidth,
        int blockHeight,
        int blockDepth,
        int blockBytes,
        int colorModel,
        bool isSrgb,
        Ktx2SampleInfo[] samples)
    {
        this.VkFormat = vkFormat;
        this.TexelFormat = texelFormat;
        this.BlockWidth = blockWidth;
        this.BlockHeight = blockHeight;
        this.BlockDepth = blockDepth;
        this.BlockBytes = blockBytes;
        this.ColorModel = colorModel;
        this.IsSrgb = isSrgb;
        this.Samples = samples;
    }

    /// <summary>
    ///     Gets the Vulkan format value.
    /// </summary>
    internal VkFormat VkFormat { get; }

    /// <summary>
    ///     Gets the texel format the container stores.
    /// </summary>
    internal TexelFormat TexelFormat { get; }

    /// <summary>
    ///     Gets the texel block width.
    /// </summary>
    internal int BlockWidth { get; }

    /// <summary>
    ///     Gets the texel block height.
    /// </summary>
    internal int BlockHeight { get; }

    /// <summary>
    ///     Gets the texel block depth.
    /// </summary>
    internal int BlockDepth { get; }

    /// <summary>
    ///     Gets the texel block size, in bytes.
    /// </summary>
    internal int BlockBytes { get; }

    /// <summary>
    ///     Gets the <c>KHR_DF_MODEL</c> value of the descriptor block.
    /// </summary>
    internal int ColorModel { get; }

    /// <summary>
    ///     Gets a value indicating whether the format stores sRGB encoded values.
    /// </summary>
    internal bool IsSrgb { get; }

    /// <summary>
    ///     Gets the sample descriptors, in file order.
    /// </summary>
    internal Ktx2SampleInfo[] Samples { get; }
}
