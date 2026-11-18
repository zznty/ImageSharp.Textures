using System.Buffers.Binary;
using ImageSharp.Textures.Formats.Dds;
using ImageSharp.Textures.Formats.Dds.Enums;
using ImageSharp.Textures.Tests.TestUtilities;
using ImageSharp.Textures.Tests.TestUtilities.Dds;

namespace ImageSharp.Textures.Tests.Formats.Dds;

[Trait("Category", "DdsDecode")]
public class DdsCubemapIdentityTests
{
    private const uint CubeMapCaps2 = 0x200 | 0xFC00;

    private static readonly TextureConfiguration Configuration = TextureConfiguration.Default;

    [Fact]
    public void LegacyCubeMapKeepsFaceIdentityOnLoad()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateLegacy(DdsFourCc.DXT1, 4, 4, 8);
        builder.IsCubeMap = true;
        builder.WithDerivedHeaderFields();
        builder.AddPatternPayloads();

        using Texture texture = Decode(builder.Build());

        Assert.Equal(6, texture.MipLevels[0].Surfaces.Count);

        for (int face = 0; face < 6; face++)
        {
            AssertPayloadFilledWith(texture.MipLevels[0].Surfaces[face], (byte)face);
        }

        DdsTextureMetadata metadata = texture.Metadata.GetFormatMetadata(DdsFormat.Instance);
        Assert.False(metadata.HasDx10Header);
        Assert.Equal(DdsFourCc.DXT1, metadata.FourCc);
        Assert.True(
            metadata.ResourceMiscFlags.HasFlag(D3d10ResourceMiscFlags.TextureCube),
            "A legacy cube map must report the cube map flag after load.");
        Assert.Equal(1U, metadata.ArraySize);
        Assert.Equal(1U, metadata.MipMapCount);
    }

    [Fact]
    public void LegacyCubeMapStaysACubeMapWhenItIsSavedAgain()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateLegacy(DdsFourCc.DXT1, 4, 4, 8);
        builder.IsCubeMap = true;
        builder.WithDerivedHeaderFields();
        builder.AddPatternPayloads();

        using Texture loaded = Decode(builder.Build());
        byte[] saved = Encode(loaded, new DdsEncodingOptions());

        // The re-save uses the legacy DXT1 path because the surface format is BC1_UNorm and no DX10 header was
        // requested; the cube map must still be declared through caps2.
        Assert.Equal(128 + (6 * 8), saved.Length);
        Assert.Equal(CubeMapCaps2, BinaryPrimitives.ReadUInt32LittleEndian(saved.AsSpan(112)));

        using Texture reloaded = Decode(saved);

        Assert.Equal(6, reloaded.MipLevels[0].Surfaces.Count);
        Assert.Equal(
            D3d10ResourceMiscFlags.TextureCube,
            reloaded.Metadata.GetFormatMetadata(DdsFormat.Instance).ResourceMiscFlags);

        for (int face = 0; face < 6; face++)
        {
            Assert.Equal(
                loaded.MipLevels[0].Surfaces[face].GetPayload().ToArray(),
                reloaded.MipLevels[0].Surfaces[face].GetPayload().ToArray());
        }
    }

    [Fact]
    public void LegacyCubeMapWithMipsAndArrayIsMappedIntoTheMetadata()
    {
        DdsFileBuilder builder = DdsFileBuilder.CreateDx10((uint)DxgiFormat.BC1_UNorm, 4, 4, 8);
        builder.IsCubeMap = true;
        builder.ArraySize = 2;
        builder.MipLevelCount = 2;
        builder.Dx10MiscFlags = 0;
        builder.WithDerivedHeaderFields();
        builder.AddPatternPayloads();

        using Texture texture = Decode(builder.Build());

        Assert.Equal(2, texture.MipLevels.Count);
        Assert.Equal(12, texture.MipLevels[0].Surfaces.Count);
        Assert.Equal(12, texture.MipLevels[1].Surfaces.Count);

        DdsTextureMetadata metadata = texture.Metadata.GetFormatMetadata(DdsFormat.Instance);
        Assert.True(metadata.ResourceMiscFlags.HasFlag(D3d10ResourceMiscFlags.TextureCube));
        Assert.Equal(2U, metadata.ArraySize);
    }

    [Fact]
    public void AnotherContainersFaceLayoutDrivesTheDdsCubeMapHeader()
    {
        TextureConfiguration configuration = TestTextureFactory.CreateConfiguration();
        using Texture texture = TestTextureFactory.CreateTexture(
            configuration,
            4,
            4,
            6,
            TexelFormat.Bc7_UNorm);

        TestFaceLayoutMetadata claim = texture.Metadata.GetFormatMetadata(TestFaceLayoutFormat.Instance);
        claim.FaceCount = 6;

        Assert.DoesNotContain(
            texture.Metadata.FormatMetadataEntries,
            metadata => metadata is DdsTextureMetadata);

        byte[] saved = Encode(texture, new DdsEncodingOptions { TargetTexelFormat = TexelFormat.Bc7_UNorm });

        Assert.Equal(CubeMapCaps2, BinaryPrimitives.ReadUInt32LittleEndian(saved.AsSpan(112)));
        Assert.Equal(0x4U, BinaryPrimitives.ReadUInt32LittleEndian(saved.AsSpan(136)));
        Assert.Equal(1U, BinaryPrimitives.ReadUInt32LittleEndian(saved.AsSpan(140)));
        Assert.Equal((uint)DxgiFormat.BC7_UNorm, BinaryPrimitives.ReadUInt32LittleEndian(saved.AsSpan(128)));

        using Texture reloaded = Decode(saved);

        Assert.Equal(6, reloaded.MipLevels[0].Surfaces.Count);
        Assert.Equal(
            D3d10ResourceMiscFlags.TextureCube,
            reloaded.Metadata.GetFormatMetadata(DdsFormat.Instance).ResourceMiscFlags);

        for (int face = 0; face < 6; face++)
        {
            Assert.Equal(
                texture.MipLevels[0].Surfaces[face].GetPayload().ToArray(),
                reloaded.MipLevels[0].Surfaces[face].GetPayload().ToArray());
        }

        Assert.Equal(saved, Encode(reloaded, new DdsEncodingOptions()));
    }

    [Fact]
    public void AnotherContainersLayerLayoutDrivesTheDdsArraySize()
    {
        TextureConfiguration configuration = TestTextureFactory.CreateConfiguration();
        using Texture texture = TestTextureFactory.CreateTexture(
            configuration,
            2,
            2,
            3,
            TexelFormat.Bc7_UNorm);

        TestFaceLayoutMetadata claim = texture.Metadata.GetFormatMetadata(TestFaceLayoutFormat.Instance);
        claim.FaceCount = 1;

        byte[] saved = Encode(texture, new DdsEncodingOptions());

        Assert.Equal(0U, BinaryPrimitives.ReadUInt32LittleEndian(saved.AsSpan(112)) & 0x200U);
        Assert.Equal(3U, BinaryPrimitives.ReadUInt32LittleEndian(saved.AsSpan(140)));

        using Texture reloaded = Decode(saved);
        Assert.Equal(3, reloaded.MipLevels[0].Surfaces.Count);
    }

    [Fact]
    public void ConflictingFaceClaimsAreRejected()
    {
        TextureConfiguration configuration = TestTextureFactory.CreateConfiguration();
        using Texture texture = TestTextureFactory.CreateTexture(
            configuration,
            4,
            4,
            6,
            TexelFormat.Bc7_UNorm);

        texture.Metadata.GetFormatMetadata(DdsFormat.Instance).ResourceMiscFlags = D3d10ResourceMiscFlags.TextureCube;
        TestFaceLayoutMetadata claim = texture.Metadata.GetFormatMetadata(TestFaceLayoutFormat.Instance);
        claim.FaceCount = 1;

        using MemoryStream stream = new();

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() => texture.Save(stream, new DdsTextureEncoder()));

        Assert.Contains("disagrees about the cube map layout", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(DdsTextureMetadata), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(TestFaceLayoutMetadata), exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, stream.Length);
    }

    [Fact]
    public void ClaimsThatContradictTheSurfaceCountAreRejected()
    {
        TextureConfiguration configuration = TestTextureFactory.CreateConfiguration();
        using Texture texture = TestTextureFactory.CreateTexture(
            configuration,
            4,
            4,
            2,
            TexelFormat.Bc7_UNorm);

        TestFaceLayoutMetadata claim = texture.Metadata.GetFormatMetadata(TestFaceLayoutFormat.Instance);
        claim.FaceCount = 6;

        using MemoryStream stream = new();

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() => texture.Save(stream, new DdsTextureEncoder()));

        Assert.Contains("holds 2 surfaces", exception.Message, StringComparison.Ordinal);
        Assert.Contains("6 face(s)", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(TestFaceLayoutMetadata), exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, stream.Length);
    }

    private static Texture Decode(byte[] file)
    {
        using MemoryStream stream = new(file);
        return new DdsTextureDecoder().Decode(Configuration, stream);
    }

    private static byte[] Encode(Texture texture, DdsEncodingOptions options)
    {
        using MemoryStream stream = new();
        texture.Save(stream, new DdsTextureEncoder(options));
        return stream.ToArray();
    }

    private static void AssertPayloadFilledWith(TextureSurface surface, byte expected)
    {
        ReadOnlySpan<byte> payload = surface.GetPayload();

        for (int i = 0; i < payload.Length; i++)
        {
            Assert.True(
                payload[i] == expected,
                $"Payload byte {i} of the face is 0x{payload[i]:X2}; expected 0x{expected:X2}.");
        }
    }
}
