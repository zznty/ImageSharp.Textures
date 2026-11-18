using ImageSharp.Textures.Formats.Ktx2;
using ImageSharp.Textures.Formats.Ktx2.Enums;

namespace ImageSharp.Textures.Tests.Formats.Ktx2;

[Trait("Category", "Ktx2")]
public class Ktx2DataFormatDescriptorTests
{
    [Theory]
    [InlineData(TexelFormat.R8G8B8A8_UNorm)]
    [InlineData(TexelFormat.Bc1_UNorm)]
    [InlineData(TexelFormat.Bc1_UnormSrgb)]
    [InlineData(TexelFormat.Bc4_UNorm)]
    [InlineData(TexelFormat.Bc5_UNorm)]
    [InlineData(TexelFormat.Bc7_UNorm)]
    [InlineData(TexelFormat.Bc7_UnormSrgb)]
    public void CreateWritesTheGoldenDescriptorForEveryTexelFormat(TexelFormat texelFormat)
    {
        string expectedHex = texelFormat switch
        {
            TexelFormat.R8G8B8A8_UNorm =>
                "5C000000" + "00000000" + "02005800" + "01010100" + "00000000" + "04000000" + "00000000"
                + "000007000000000000000000FF000000"
                + "080007010000000000000000FF000000"
                + "100007020000000000000000FF000000"
                + "1800070F0000000000000000FF000000",
            TexelFormat.Bc1_UNorm =>
                "2C000000" + "00000000" + "02002800" + "80010100" + "03030000" + "08000000" + "00000000"
                + "00003F010000000000000000FFFFFFFF",
            TexelFormat.Bc1_UnormSrgb =>
                "2C000000" + "00000000" + "02002800" + "80010200" + "03030000" + "08000000" + "00000000"
                + "00003F010000000000000000FFFFFFFF",
            TexelFormat.Bc4_UNorm =>
                "2C000000" + "00000000" + "02002800" + "83010100" + "03030000" + "08000000" + "00000000"
                + "00003F000000000000000000FFFFFFFF",
            TexelFormat.Bc5_UNorm =>
                "3C000000" + "00000000" + "02003800" + "84010100" + "03030000" + "10000000" + "00000000"
                + "00003F000000000000000000FFFFFFFF"
                + "40003F010000000000000000FFFFFFFF",
            TexelFormat.Bc7_UNorm =>
                "2C000000" + "00000000" + "02002800" + "86010100" + "03030000" + "10000000" + "00000000"
                + "00007F000000000000000000FFFFFFFF",
            _ =>
                "2C000000" + "00000000" + "02002800" + "86010200" + "03030000" + "10000000" + "00000000"
                + "00007F000000000000000000FFFFFFFF"
        };

        Ktx2FormatInfo formatInfo = Ktx2TexelFormats.GetFormatInfo(texelFormat);

        byte[] dfd = Ktx2DataFormatDescriptor.Create(formatInfo);

        Assert.Equal(expectedHex, Convert.ToHexString(dfd));
    }

    [Theory]
    [InlineData(TexelFormat.R8G8B8A8_UNorm, 92)]
    [InlineData(TexelFormat.Bc1_UNorm, 44)]
    [InlineData(TexelFormat.Bc4_UNorm, 44)]
    [InlineData(TexelFormat.Bc5_UNorm, 60)]
    [InlineData(TexelFormat.Bc7_UNorm, 44)]
    public void CreateWritesTheExpectedTotalSize(TexelFormat texelFormat, int expectedLength)
    {
        byte[] dfd = Ktx2DataFormatDescriptor.Create(Ktx2TexelFormats.GetFormatInfo(texelFormat));

        Assert.Equal(expectedLength, dfd.Length);
        Assert.Equal((uint)expectedLength, BitConverter.ToUInt32(dfd, 0));
    }

    [Fact]
    public void CreateWritesTheDescriptorBlockSizeOfEverySampleCount()
    {
        byte[] bc1 = Ktx2DataFormatDescriptor.Create(Ktx2TexelFormats.GetFormatInfo(TexelFormat.Bc1_UNorm));
        byte[] bc5 = Ktx2DataFormatDescriptor.Create(Ktx2TexelFormats.GetFormatInfo(TexelFormat.Bc5_UNorm));

        Assert.Equal(40, BitConverter.ToUInt16(bc1, 10));
        Assert.Equal(56, BitConverter.ToUInt16(bc5, 10));
    }

    [Theory]
    [InlineData(TexelFormat.R8G8B8A8_UNorm, VkFormat.VK_FORMAT_R8G8B8A8_UNORM)]
    [InlineData(TexelFormat.Bc1_UNorm, VkFormat.VK_FORMAT_BC1_RGBA_UNORM_BLOCK)]
    [InlineData(TexelFormat.Bc1_UnormSrgb, VkFormat.VK_FORMAT_BC1_RGBA_SRGB_BLOCK)]
    [InlineData(TexelFormat.Bc4_UNorm, VkFormat.VK_FORMAT_BC4_UNORM_BLOCK)]
    [InlineData(TexelFormat.Bc5_UNorm, VkFormat.VK_FORMAT_BC5_UNORM_BLOCK)]
    [InlineData(TexelFormat.Bc7_UNorm, VkFormat.VK_FORMAT_BC7_UNORM_BLOCK)]
    [InlineData(TexelFormat.Bc7_UnormSrgb, VkFormat.VK_FORMAT_BC7_SRGB_BLOCK)]
    public void GetVkFormatUsesTheMappingTable(TexelFormat texelFormat, VkFormat expectedVkFormat) =>
        Assert.Equal(expectedVkFormat, Ktx2TexelFormats.GetVkFormat(texelFormat));

    [Theory]
    [InlineData(VkFormat.VK_FORMAT_R8G8B8A8_UNORM, TexelFormat.R8G8B8A8_UNorm)]
    [InlineData(VkFormat.VK_FORMAT_BC1_RGBA_UNORM_BLOCK, TexelFormat.Bc1_UNorm)]
    [InlineData(VkFormat.VK_FORMAT_BC1_RGB_UNORM_BLOCK, TexelFormat.Bc1_UNorm)]
    [InlineData(VkFormat.VK_FORMAT_BC1_RGBA_SRGB_BLOCK, TexelFormat.Bc1_UnormSrgb)]
    [InlineData(VkFormat.VK_FORMAT_BC1_RGB_SRGB_BLOCK, TexelFormat.Bc1_UnormSrgb)]
    [InlineData(VkFormat.VK_FORMAT_BC4_UNORM_BLOCK, TexelFormat.Bc4_UNorm)]
    [InlineData(VkFormat.VK_FORMAT_BC5_UNORM_BLOCK, TexelFormat.Bc5_UNorm)]
    [InlineData(VkFormat.VK_FORMAT_BC7_UNORM_BLOCK, TexelFormat.Bc7_UNorm)]
    [InlineData(VkFormat.VK_FORMAT_BC7_SRGB_BLOCK, TexelFormat.Bc7_UnormSrgb)]
    public void TryGetTexelFormatAcceptsTheBc1Aliases(VkFormat vkFormat, TexelFormat expected)
    {
        Assert.True(Ktx2TexelFormats.TryGetTexelFormat(vkFormat, out TexelFormat texelFormat));
        Assert.Equal(expected, texelFormat);
    }

    [Fact]
    public void ValidateAcceptsTheDescriptorsItWrites()
    {
        foreach (TexelFormat texelFormat in new[]
                 {
                     TexelFormat.R8G8B8A8_UNorm, TexelFormat.Bc1_UNorm, TexelFormat.Bc1_UnormSrgb,
                     TexelFormat.Bc4_UNorm, TexelFormat.Bc5_UNorm, TexelFormat.Bc7_UNorm, TexelFormat.Bc7_UnormSrgb
                 })
        {
            Ktx2FormatInfo info = Ktx2TexelFormats.GetFormatInfo(texelFormat);
            byte[] dfd = Ktx2DataFormatDescriptor.Create(info);

            Ktx2DataFormatDescriptor.Validate(dfd, info, info.VkFormat);
        }
    }

    [Fact]
    public void ValidateRejectsAVkFormatMismatch()
    {
        Ktx2FormatInfo bc1 = Ktx2TexelFormats.GetFormatInfo(TexelFormat.Bc1_UNorm);
        byte[] dfd = Ktx2DataFormatDescriptor.Create(bc1);

        InvalidImageContentException exception = Assert.Throws<InvalidImageContentException>(() =>
            Ktx2DataFormatDescriptor.Validate(dfd, Ktx2TexelFormats.GetFormatInfo(TexelFormat.Bc5_UNorm),
                bc1.VkFormat));

        Assert.Contains("samples", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateRejectsATotalSizeMismatch()
    {
        Ktx2FormatInfo info = Ktx2TexelFormats.GetFormatInfo(TexelFormat.R8G8B8A8_UNorm);
        byte[] dfd = Ktx2DataFormatDescriptor.Create(info);
        BitConverter.GetBytes(96u).CopyTo(dfd, 0);

        Assert.Throws<InvalidImageContentException>(() => Ktx2DataFormatDescriptor.Validate(dfd, info, info.VkFormat));
    }

    [Fact]
    public void ValidateRejectsAColorModelMismatch()
    {
        Ktx2FormatInfo info = Ktx2TexelFormats.GetFormatInfo(TexelFormat.R8G8B8A8_UNorm);
        byte[] dfd = Ktx2DataFormatDescriptor.Create(info);
        dfd[12] = 128;

        InvalidImageContentException exception =
            Assert.Throws<InvalidImageContentException>(() =>
                Ktx2DataFormatDescriptor.Validate(dfd, info, info.VkFormat));
        Assert.Contains("color model", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateRejectsABytesPlaneMismatch()
    {
        Ktx2FormatInfo info = Ktx2TexelFormats.GetFormatInfo(TexelFormat.Bc7_UNorm);
        byte[] dfd = Ktx2DataFormatDescriptor.Create(info);
        dfd[20] = 8;

        InvalidImageContentException exception =
            Assert.Throws<InvalidImageContentException>(() =>
                Ktx2DataFormatDescriptor.Validate(dfd, info, info.VkFormat));
        Assert.Contains("bytesPlane", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateRejectsATexelBlockDimensionMismatch()
    {
        Ktx2FormatInfo info = Ktx2TexelFormats.GetFormatInfo(TexelFormat.Bc1_UNorm);
        byte[] dfd = Ktx2DataFormatDescriptor.Create(info);
        dfd[16] = 0;

        InvalidImageContentException exception =
            Assert.Throws<InvalidImageContentException>(() =>
                Ktx2DataFormatDescriptor.Validate(dfd, info, info.VkFormat));
        Assert.Contains("texel block dimensions", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateRejectsAChannelIdMismatch()
    {
        Ktx2FormatInfo info = Ktx2TexelFormats.GetFormatInfo(TexelFormat.R8G8B8A8_UNorm);
        byte[] dfd = Ktx2DataFormatDescriptor.Create(info);
        dfd[28 + 3] = 1;

        InvalidImageContentException exception =
            Assert.Throws<InvalidImageContentException>(() =>
                Ktx2DataFormatDescriptor.Validate(dfd, info, info.VkFormat));
        Assert.Contains("sample 0", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateRejectsASampleUpperMismatch()
    {
        Ktx2FormatInfo info = Ktx2TexelFormats.GetFormatInfo(TexelFormat.Bc1_UNorm);
        byte[] dfd = Ktx2DataFormatDescriptor.Create(info);
        BitConverter.GetBytes(0u).CopyTo(dfd, 28 + 12);

        InvalidImageContentException exception =
            Assert.Throws<InvalidImageContentException>(() =>
                Ktx2DataFormatDescriptor.Validate(dfd, info, info.VkFormat));
        Assert.Contains("bounds", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateRejectsAnSrgbTransferMismatch()
    {
        Ktx2FormatInfo info = Ktx2TexelFormats.GetFormatInfo(TexelFormat.Bc7_UnormSrgb);
        byte[] dfd = Ktx2DataFormatDescriptor.Create(info);
        dfd[14] = Ktx2DataFormatDescriptor.TransferLinear;

        InvalidImageContentException exception =
            Assert.Throws<InvalidImageContentException>(() =>
                Ktx2DataFormatDescriptor.Validate(dfd, info, info.VkFormat));
        Assert.Contains("transfer function", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateAcceptsAnOpaqueBc1ChannelIdForTheAlphaPresentVariant()
    {
        Ktx2FormatInfo info = Ktx2TexelFormats.GetFormatInfo(TexelFormat.Bc1_UNorm);
        byte[] dfd = Ktx2DataFormatDescriptor.Create(info);
        dfd[28 + 3] = 0;

        Ktx2DataFormatDescriptor.Validate(dfd, info, info.VkFormat);
    }
}
