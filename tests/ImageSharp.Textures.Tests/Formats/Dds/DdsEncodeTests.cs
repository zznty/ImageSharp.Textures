using System.Buffers.Binary;
using ImageSharp.Textures.Formats.Dds;
using ImageSharp.Textures.Formats.Dds.Enums;
using ImageSharp.Textures.Tests.TestUtilities;
using ImageSharp.Textures.Tests.TestUtilities.Dds;

namespace ImageSharp.Textures.Tests.Formats.Dds;

[Trait("Category", "DdsEncode")]
public class DdsEncodeTests
{
    /// <summary>
    ///     The exact 148 header bytes a 4x4 single level R8G8B8A8_UNORM texture must produce: magic, header
    ///     (CAPS|HEIGHT|WIDTH|PITCH|PIXELFORMAT, pitch 16, one mip), the DX10 pixel format, caps, and the DX10
    ///     header (DXGI 28, TEXTURE2D, one array element, alpha mode STRAIGHT).
    /// </summary>
    private const string Rgba8HeaderHex =
        "44445320" + "7C000000" + "0F100200" + "04000000" + "04000000" + "10000000" + "01000000" + "01000000"
        + "0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"
        + "20000000" + "04000000" + "44583130" + "00000000" + "00000000" + "00000000" + "00000000" + "00000000"
        + "00100000" + "00000000" + "00000000" + "00000000" + "00000000"
        + "1C000000" + "03000000" + "00000000" + "01000000" + "01000000";

    private static readonly TextureConfiguration Configuration = TextureConfiguration.Default;

    [Fact]
    public void DefaultOptionsExposeTheFrozenDefaults()
    {
        DdsTextureEncoder encoder = new();
        DdsEncodingOptions options = encoder.Options;

        Assert.Same(DdsFormat.Instance, encoder.Format);
        Assert.Null(options.TargetTexelFormat);
        Assert.Equal(Bc7EncodingQuality.Default, options.Bc7Quality);
        Assert.False(options.UseDx10Header);
        Assert.Equal(0.5f, options.Bc1AlphaThreshold);
    }

    [Fact]
    public void ConstructorKeepsTheSuppliedOptions()
    {
        DdsEncodingOptions options = new()
        {
            TargetTexelFormat = TexelFormat.R8G8B8A8_UNorm,
            Bc7Quality = Bc7EncodingQuality.Quick,
            UseDx10Header = true,
            Bc1AlphaThreshold = 0.25f
        };

        DdsTextureEncoder encoder = new(options);

        Assert.Same(options, encoder.Options);
        Assert.Equal(TexelFormat.R8G8B8A8_UNorm, encoder.Options.TargetTexelFormat);
        Assert.Equal(Bc7EncodingQuality.Quick, encoder.Options.Bc7Quality);
        Assert.True(encoder.Options.UseDx10Header);
        Assert.Equal(0.25f, encoder.Options.Bc1AlphaThreshold);
    }

    [Fact]
    public void Rgba8HeaderMatchesTheGoldenBytes()
    {
        using Texture texture = TestTextureFactory.CreateTexture(TestTextureFactory.CreateConfiguration(), 4, 4);

        byte[] encoded = Encode(texture);

        Assert.Equal(Rgba8HeaderHex, Convert.ToHexString(encoded.AsSpan(0, 148)));
        Assert.Equal(148 + (4 * 4 * 4), encoded.Length);
    }

    [Fact]
    public void EncodeWritesTheSurfacePayloadVerbatim()
    {
        using Texture texture = TestTextureFactory.CreateTexture(TestTextureFactory.CreateConfiguration(), 4, 4);
        byte[] payload = texture.MipLevels[0].Surfaces[0].GetPayload().ToArray();

        byte[] encoded = Encode(texture);

        Assert.Equal(payload, encoded.AsSpan(148).ToArray());
    }

    [Fact]
    public void Rgba8RoundTripIsByteExact()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 8, 8, 0);
        builder.MipLevelCount = 3;
        builder.WithDerivedHeaderFields();
        builder.AddPatternPayloads();

        using Texture decoded = Decode(builder.Build());
        byte[] encoded = Encode(decoded);
        using Texture roundTripped = Decode(encoded);

        AssertPayloadsEqual(decoded, roundTripped);
    }

    [Fact]
    public void EncodeTwiceProducesIdenticalBytes()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 8, 8, 0);
        builder.MipLevelCount = 2;
        builder.WithDerivedHeaderFields();
        builder.AddPatternPayloads();

        using Texture texture = Decode(builder.Build());

        Assert.Equal(Encode(texture), Encode(texture));
    }

    [Fact]
    public void EncodeDoesNotModifyTheTexture()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 4, 4, 0);
        builder.AddPatternPayloads();

        using Texture texture = Decode(builder.Build());
        byte[] before = texture.MipLevels[0].Surfaces[0].GetPayload().ToArray();

        Encode(texture);

        Assert.Equal(before, texture.MipLevels[0].Surfaces[0].GetPayload().ToArray());
    }

    [Fact]
    public void EditsMadeThroughCopyFromAreWritten()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 2, 2, 0);
        builder.AddPatternPayloads();

        using Texture texture = Decode(builder.Build());
        TextureSurface surface = texture.MipLevels[0].Surfaces[0];

        using (Image<Rgba32> edited = surface.ToImage<Rgba32>())
        {
            edited.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    accessor.GetRowSpan(y).Fill(new Rgba32(9, 8, 7, 6));
                }
            });

            surface.CopyFrom(edited);
        }

        using Texture reloaded = Decode(Encode(texture));
        using Image<Rgba32> actual = reloaded.MipLevels[0].Surfaces[0].ToImage<Rgba32>();

        for (int y = 0; y < actual.Height; y++)
        {
            for (int x = 0; x < actual.Width; x++)
            {
                Assert.Equal(new Rgba32(9, 8, 7, 6), actual[x, y]);
            }
        }
    }

    [Fact]
    public void ArrayRoundTripPreservesLayerPayloads()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 2, 2, 0);
        builder.ArraySize = 3;
        builder.WithDerivedHeaderFields();
        builder.AddPatternPayloads();

        using Texture decoded = Decode(builder.Build());
        using Texture roundTripped = Decode(Encode(decoded));

        Assert.Equal(3, roundTripped.MipLevels[0].Surfaces.Count);
        AssertPayloadsEqual(decoded, roundTripped);
    }

    [Fact]
    public void CubeMapRoundTripPreservesFaceOrder()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 4, 4, 0);
        builder.IsCubeMap = true;
        builder.WithDerivedHeaderFields();
        builder.AddPatternPayloads();

        using Texture decoded = Decode(builder.Build());
        byte[] encoded = Encode(decoded);
        using Texture roundTripped = Decode(encoded);

        Assert.Equal(6, roundTripped.MipLevels[0].Surfaces.Count);
        AssertPayloadsEqual(decoded, roundTripped);
        Assert.Equal(
            D3d10ResourceMiscFlags.TextureCube,
            roundTripped.Metadata.GetFormatMetadata(DdsFormat.Instance).ResourceMiscFlags);

        // The cube map flag, the array size and the caps bits must survive the round trip.
        DdsHeaderFields header = DdsHeaderFields.Parse(encoded);
        Assert.Equal(0x1008u, header.Caps1);
        Assert.Equal(0x200u | 0xFE00u, header.Caps2);
        Assert.Equal(0x4u, header.Dx10MiscFlags);
        Assert.Equal(1u, header.ArraySize);
    }

    [Fact]
    public void VolumeRoundTripPreservesDepthSlices()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 4, 4, 0);
        builder.IsVolume = true;
        builder.Depth = 4;
        builder.MipLevelCount = 3;
        builder.WithDerivedHeaderFields();
        builder.AddPatternPayloads();

        using Texture decoded = Decode(builder.Build());
        byte[] encoded = Encode(decoded);
        using Texture roundTripped = Decode(encoded);

        Assert.Equal(3, roundTripped.MipLevels.Count);
        Assert.Equal(4, roundTripped.MipLevels[0].Depth);
        AssertPayloadsEqual(decoded, roundTripped);

        DdsHeaderFields header = DdsHeaderFields.Parse(encoded);
        Assert.Equal(4u, header.Depth);
        Assert.Equal(0x200000u, header.Caps2 & 0x200000u);
        Assert.Equal(4u, header.Dx10ResourceDimension);
    }

    [Fact]
    public void SavePathWritesALoadableDds()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 4, 4, 0);
        builder.AddPatternPayloads();

        using Texture texture = Decode(builder.Build());

        string directory = Path.Combine(Path.GetTempPath(), $"imagesharp-textures-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "saved.dds");

        try
        {
            texture.Save(path);

            using Texture reloaded = Texture.Load(path);
            AssertPayloadsEqual(texture, reloaded);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void SaveStreamWithFormatWritesALoadableDds()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 4, 4, 0);
        builder.AddPatternPayloads();

        using Texture texture = Decode(builder.Build());
        using MemoryStream stream = new();

        texture.Save(stream, DdsFormat.Instance);

        using Texture reloaded = Decode(stream.ToArray());
        AssertPayloadsEqual(texture, reloaded);
    }

    [Fact]
    public async Task SaveAsyncWritesALoadableDds()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.R8G8B8A8_UNorm, 4, 4, 0);
        builder.AddPatternPayloads();

        using Texture texture = Decode(builder.Build());
        using MemoryStream stream = new();

        await texture.SaveAsync(stream, DdsFormat.Instance);

        using Texture reloaded = Decode(stream.ToArray());
        AssertPayloadsEqual(texture, reloaded);
    }

    [Fact]
    public void SavePathRejectsAnExtensionServedByTwoEncoders()
    {
        TextureConfiguration configuration = TestTextureFactory.CreateConfiguration();

        // Format registrations are keyed by type, so the ambiguity needs two distinct format types that
        // both claim the extension and both have an encoder.
        TestTextureFormat alias = new("ALIAS", "image/x-alias", ".dds");
        configuration.Formats.AddFormat(
            DdsFormat.Instance,
            new TestTextureDecoder(DdsFormat.Instance),
            new TestTextureEncoder(DdsFormat.Instance));
        configuration.Formats.AddFormat(alias, new TestTextureDecoder(alias), new TestTextureEncoder(alias));

        using Texture texture = TestTextureFactory.CreateTexture(configuration, 2, 2);
        string path = Path.Combine(Path.GetTempPath(), $"imagesharp-textures-{Guid.NewGuid():N}.dds");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => texture.Save(path));
        Assert.Contains("More than one texture encoder", exception.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void SavePathRejectsAnExtensionWithoutAnEncoder()
    {
        TextureConfiguration configuration = TestTextureFactory.CreateConfiguration();
        TestTextureFormat format = new("TEST", "image/x-test", ".test");
        configuration.Formats.AddFormat(format, new TestTextureDecoder(format));

        using Texture texture = TestTextureFactory.CreateTexture(configuration, 2, 2);
        string path = Path.Combine(Path.GetTempPath(), $"imagesharp-textures-{Guid.NewGuid():N}.test");

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() => texture.Save(path));
        Assert.Contains("No encoder", exception.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void EncodingATextureWithoutMipLevelsThrows()
    {
        using Texture texture = new(TestTextureFactory.CreateConfiguration(), new TextureMetadata());
        using MemoryStream stream = new();

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() => new DdsTextureEncoder().Encode(texture, stream));
        Assert.Contains("mip levels", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, stream.Length);
    }

    [Fact]
    public void EncodingATextureWithoutSurfacesThrows()
    {
        TextureConfiguration configuration = TestTextureFactory.CreateConfiguration();
        using Texture texture = new(configuration, new TextureMetadata());
        texture.AddMipLevel(4, 4, 1);
        using MemoryStream stream = new();

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() => new DdsTextureEncoder().Encode(texture, stream));
        Assert.Contains("surfaces", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EncodingANonHalvingMipChainThrows()
    {
        TextureConfiguration configuration = TestTextureFactory.CreateConfiguration();
        using Texture texture = TestTextureFactory.CreateTexture(configuration, 8, 8);
        texture.AddMipLevel(3, 3, 1);
        using MemoryStream stream = new();

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() => new DdsTextureEncoder().Encode(texture, stream));
        Assert.Contains("halving mip chain", exception.Message, StringComparison.Ordinal);
        Assert.Contains("3x3", exception.Message, StringComparison.Ordinal);
    }

    private static Texture Decode(byte[] file)
    {
        using MemoryStream stream = new(file);
        return new DdsTextureDecoder().Decode(Configuration, stream);
    }

    private static byte[] Encode(Texture texture)
    {
        using MemoryStream stream = new();
        new DdsTextureEncoder().Encode(texture, stream);
        return stream.ToArray();
    }

    private static void AssertPayloadsEqual(Texture expected, Texture actual)
    {
        Assert.Equal(expected.MipLevels.Count, actual.MipLevels.Count);

        for (int level = 0; level < expected.MipLevels.Count; level++)
        {
            TextureMipLevel expectedLevel = expected.MipLevels[level];
            TextureMipLevel actualLevel = actual.MipLevels[level];

            Assert.Equal(expectedLevel.Width, actualLevel.Width);
            Assert.Equal(expectedLevel.Height, actualLevel.Height);
            Assert.Equal(expectedLevel.Depth, actualLevel.Depth);
            Assert.Equal(expectedLevel.Surfaces.Count, actualLevel.Surfaces.Count);

            for (int index = 0; index < expectedLevel.Surfaces.Count; index++)
            {
                Assert.Equal(
                    expectedLevel.Surfaces[index].GetPayload().ToArray(),
                    actualLevel.Surfaces[index].GetPayload().ToArray());
            }
        }
    }

    /// <summary>
    ///     Reads the header fields the encode tests assert on back out of an encoded file.
    /// </summary>
    private readonly struct DdsHeaderFields
    {
        private DdsHeaderFields(uint caps1, uint caps2, uint depth, uint dx10ResourceDimension, uint dx10MiscFlags,
            uint arraySize)
        {
            this.Caps1 = caps1;
            this.Caps2 = caps2;
            this.Depth = depth;
            this.Dx10ResourceDimension = dx10ResourceDimension;
            this.Dx10MiscFlags = dx10MiscFlags;
            this.ArraySize = arraySize;
        }

        internal uint Caps1 { get; }

        internal uint Caps2 { get; }

        internal uint Depth { get; }

        internal uint Dx10ResourceDimension { get; }

        internal uint Dx10MiscFlags { get; }

        internal uint ArraySize { get; }

        internal static DdsHeaderFields Parse(byte[] file)
            => new(
                BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(108)),
                BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(112)),
                BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(24)),
                BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(132)),
                BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(136)),
                BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(140)));
    }
}
