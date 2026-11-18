using System.Buffers.Binary;
using System.Text;
using ImageSharp.Textures.Formats.Dds;
using ImageSharp.Textures.Formats.Dds.Enums;
using ImageSharp.Textures.Formats.Ktx2;
using ImageSharp.Textures.Formats.Ktx;
using ImageSharp.Textures.Parity.Tests.TestUtilities;

namespace ImageSharp.Textures.Parity.Tests;

/// <summary>
///     Cross-container payload identity: a texture must keep every surface payload byte for byte, and keep the
///     layer/face/depth ordering, when it travels through DDS, KTX v1 and KTX2.
/// </summary>
/// <remarks>
///     All tests run in process and never invoke texconv. The surface collection order is layer, face, depth for
///     every container, so the surface index identifies a face; the cubemap array test is the one that would fail
///     if any container disagreed about that order.
/// </remarks>
[Trait("Category", "CrossContainer")]
public class CrossContainerTests
{
    private const int Width = 8;
    private const int Height = 8;

    /// <summary>
    ///     Gets the formats exercised by the payload identity matrix.
    /// </summary>
    public static TheoryData<TexelFormat> Formats => new()
    {
        TexelFormat.R8G8B8A8_UNorm,
        TexelFormat.Bc1_UNorm,
        TexelFormat.Bc1_UnormSrgb,
        TexelFormat.Bc4_UNorm,
        TexelFormat.Bc5_UNorm,
        TexelFormat.Bc7_UNorm,
        TexelFormat.Bc7_UnormSrgb
    };

    /// <summary>
    ///     Every supported texel format must keep its payloads and its level/surface shape across
    ///     DDS -> KTX2 -> DDS.
    /// </summary>
    /// <param name="texelFormat">The format to exercise.</param>
    [Theory]
    [MemberData(nameof(Formats))]
    public void PayloadsSurviveDdsKtx2Dds(TexelFormat texelFormat)
    {
        using ParityWorkspace workspace = ParityWorkspace.Create($"cross-format-{texelFormat}");
        using Texture original = Build(texelFormat, Width, Height);

        using Texture fromDds = SaveAndLoad(original, workspace.PathFor("first.dds"));
        AssertSameTexture(original, fromDds, texelFormat, "DDS");

        using Texture fromKtx2 = SaveAndLoad(fromDds, workspace.PathFor("hop.ktx2"));
        AssertSameTexture(original, fromKtx2, texelFormat, "KTX2");

        using Texture final = SaveAndLoad(fromKtx2, workspace.PathFor("second.dds"));
        AssertSameTexture(original, final, texelFormat, "DDS");

        Assert.Equal(1, final.MipLevels.Count);
        Assert.Equal(1, final.MipLevels[0].Surfaces.Count);
        Assert.Equal(1, final.MipLevels[0].Depth);

        workspace.MarkSuccess();
    }

    /// <summary>
    ///     A two layer BC7 cubemap array must survive DDS -> KTX2 -> KTX1 -> DDS with every face payload intact
    ///     and the face identity preserved.
    /// </summary>
    [Fact]
    public void CubemapArrayPreservesFaceIdentityAcrossDdsKtx2Ktx1Dds()
    {
        // 8x8 BC7 gives a 2x2 block grid, so each face payload is 16 bytes and a swap of any two faces is
        // visible as a different byte pattern.
        using ParityWorkspace workspace = ParityWorkspace.Create("cross-cube-array");
        using Texture original = Build(
            TexelFormat.Bc7_UNorm,
            Width,
            Height,
            1,
            2,
            6,
            declareCubeMap: true);

        Assert.Equal(12, original.MipLevels[0].Surfaces.Count);

        using Texture fromDds = SaveAndLoad(original, workspace.PathFor("source.dds"));
        AssertSameTexture(original, fromDds, TexelFormat.Bc7_UNorm, "DDS");

        using Texture fromKtx2 = SaveAndLoad(fromDds, workspace.PathFor("hop.ktx2"));
        Ktx2TextureMetadata ktx2Metadata = fromKtx2.Metadata.GetFormatMetadata(Ktx2Format.Instance);
        string ktx2Shape = $"KTX2 declared faceCount {ktx2Metadata.FaceCount} and layerCount {ktx2Metadata.LayerCount} "
                           + "for a 2 layer x 6 face cubemap array.";
        Require(ktx2Metadata.FaceCount == 6 && ktx2Metadata.LayerCount == 2, ktx2Shape);
        AssertSameTexture(original, fromKtx2, TexelFormat.Bc7_UNorm, "KTX2");

        using Texture fromKtx = SaveAndLoad(fromKtx2, workspace.PathFor("hop.ktx"));
        KtxTextureMetadata ktxMetadata = fromKtx.Metadata.GetFormatMetadata(KtxFormat.Instance);
        string ktxShape = $"KTX v1 declared numberOfFaces {ktxMetadata.FaceCount} and numberOfArrayElements "
                          + $"{ktxMetadata.ArrayElementCount} for a 2 layer x 6 face cubemap array.";
        Require(ktxMetadata.FaceCount == 6 && ktxMetadata.ArrayElementCount == 2, ktxShape);
        AssertSameTexture(original, fromKtx, TexelFormat.Bc7_UNorm, "KTX");

        // No metadata patching: the KTX v1 metadata's face layout claim must be enough for the DDS encoder.
        using Texture final = SaveAndLoad(fromKtx, workspace.PathFor("final.dds"));
        AssertSameTexture(original, final, TexelFormat.Bc7_UNorm, "DDS");

        DdsTextureMetadata ddsMetadata = final.Metadata.GetFormatMetadata(DdsFormat.Instance);
        Require(
            (ddsMetadata.ResourceMiscFlags & D3d10ResourceMiscFlags.TextureCube) != 0,
            "The final DDS header does not declare a cubemap.");
        Require(ddsMetadata.ArraySize == 2,
            $"The final DDS header declares an array size of {ddsMetadata.ArraySize}; 2 was expected.");
        Assert.Equal(12, final.MipLevels[0].Surfaces.Count);

        // Surface k of the round-tripped texture must hold the payload of surface k of the source texture.
        AssertSameTexture(original, final, TexelFormat.Bc7_UNorm, "DDS (face identity)");

        workspace.MarkSuccess();
    }

    /// <summary>
    ///     A DDS-loaded cube map must write six faces to KTX v1 and KTX2, and come back to DDS as a cube map,
    ///     without the caller setting any face option or patching metadata.
    /// </summary>
    [Fact]
    public void DdsLoadedCubeKeepsItsFaceLayoutThroughKtxAndDds()
    {
        using ParityWorkspace workspace = ParityWorkspace.Create("cross-dds-cube-seam");
        using Texture original = Build(TexelFormat.Bc7_UNorm, Width, Height, faceCount: 6, declareCubeMap: true);

        using Texture fromDds = SaveAndLoad(original, workspace.PathFor("source.dds"));
        AssertSameTexture(original, fromDds, TexelFormat.Bc7_UNorm, "DDS");
        DdsTextureMetadata sourceMetadata = fromDds.Metadata.GetFormatMetadata(DdsFormat.Instance);
        Require(
            (sourceMetadata.ResourceMiscFlags & D3d10ResourceMiscFlags.TextureCube) != 0,
            "The DDS source does not declare a cubemap; the test cannot exercise the face layout seam.");

        using Texture fromKtx2 = SaveAndLoad(fromDds, workspace.PathFor("hop.ktx2"));
        Ktx2TextureMetadata ktx2Metadata = fromKtx2.Metadata.GetFormatMetadata(Ktx2Format.Instance);
        string ktx2Shape =
            $"KTX2 declared faceCount {ktx2Metadata.FaceCount} and layerCount {ktx2Metadata.LayerCount}; "
            + "6 faces and no array layers were expected.";
        Require(ktx2Metadata.FaceCount == 6 && ktx2Metadata.LayerCount == 0, ktx2Shape);
        Assert.Equal(6, fromKtx2.MipLevels[0].Surfaces.Count);
        AssertSameTexture(original, fromKtx2, TexelFormat.Bc7_UNorm, "KTX2");

        using Texture fromKtx = SaveAndLoad(fromKtx2, workspace.PathFor("hop.ktx"));
        KtxTextureMetadata ktxMetadata = fromKtx.Metadata.GetFormatMetadata(KtxFormat.Instance);
        string ktxShape = $"KTX v1 declared numberOfFaces {ktxMetadata.FaceCount} and numberOfArrayElements "
                          + $"{ktxMetadata.ArrayElementCount}; 6 faces and no array elements were expected.";
        Require(ktxMetadata.FaceCount == 6 && ktxMetadata.ArrayElementCount == 0, ktxShape);
        AssertSameTexture(original, fromKtx, TexelFormat.Bc7_UNorm, "KTX");

        using Texture final = SaveAndLoad(fromKtx, workspace.PathFor("final.dds"));
        DdsTextureMetadata finalMetadata = final.Metadata.GetFormatMetadata(DdsFormat.Instance);
        Require(
            (finalMetadata.ResourceMiscFlags & D3d10ResourceMiscFlags.TextureCube) != 0,
            "The final DDS header does not declare a cubemap.");
        Require(finalMetadata.ArraySize == 1,
            $"The final DDS header declares an array size of {finalMetadata.ArraySize}; 1 was expected.");
        Assert.Equal(6, final.MipLevels[0].Surfaces.Count);
        AssertSameTexture(original, final, TexelFormat.Bc7_UNorm, "DDS (face layout seam)");

        workspace.MarkSuccess();
    }

    /// <summary>
    ///     A BC1 volume and a BC5 volume must keep every depth slice payload and the level depth across
    ///     DDS -> KTX2 -> DDS.
    /// </summary>
    /// <param name="texelFormat">The volume format.</param>
    [Theory]
    [InlineData(TexelFormat.Bc1_UNorm)]
    [InlineData(TexelFormat.Bc5_UNorm)]
    public void VolumeSlicesSurviveDdsKtx2Dds(TexelFormat texelFormat)
    {
        using ParityWorkspace workspace = ParityWorkspace.Create($"cross-volume-{texelFormat}");
        using Texture original = Build(texelFormat, Width, Height, 1, depth: 4);

        Assert.Equal(4, original.MipLevels[0].Depth);
        Assert.Equal(4, original.MipLevels[0].Surfaces.Count);

        using Texture fromDds = SaveAndLoad(original, workspace.PathFor("first.dds"));
        AssertSameTexture(original, fromDds, texelFormat, "DDS");
        Assert.Equal(4, fromDds.MipLevels[0].Depth);

        using Texture fromKtx2 = SaveAndLoad(fromDds, workspace.PathFor("hop.ktx2"));
        AssertSameTexture(original, fromKtx2, texelFormat, "KTX2");
        Assert.Equal(4, fromKtx2.MipLevels[0].Depth);

        using Texture final = SaveAndLoad(fromKtx2, workspace.PathFor("second.dds"));
        AssertSameTexture(original, final, texelFormat, "DDS");
        Assert.Equal(4, final.MipLevels[0].Depth);
        Assert.Equal(4, final.MipLevels[0].Surfaces.Count);

        workspace.MarkSuccess();
    }

    /// <summary>
    ///     A five level BC7 mip chain must keep every level payload across DDS -> container -> DDS.
    /// </summary>
    /// <param name="container">The intermediate container, <c>KTX</c> or <c>KTX2</c>.</param>
    [Theory]
    [InlineData("KTX")]
    [InlineData("KTX2")]
    public void MipChainPayloadsSurviveTheContainerHop(string container)
    {
        using ParityWorkspace workspace = ParityWorkspace.Create($"cross-mips-{container}");
        using Texture original = Build(TexelFormat.Bc7_UNorm, 32, 32, 5);

        Assert.Equal(5, original.MipLevels.Count);

        using Texture fromDds = SaveAndLoad(original, workspace.PathFor("first.dds"));
        AssertSameTexture(original, fromDds, TexelFormat.Bc7_UNorm, "DDS");

        using Texture fromContainer = SaveAndLoad(fromDds, workspace.PathFor($"hop.{container.ToLowerInvariant()}"));
        AssertSameTexture(original, fromContainer, TexelFormat.Bc7_UNorm, container);
        Assert.Equal(5, fromContainer.MipLevels.Count);

        using Texture final = SaveAndLoad(fromContainer, workspace.PathFor("second.dds"));
        AssertSameTexture(original, final, TexelFormat.Bc7_UNorm, "DDS");
        Assert.Equal(5, final.MipLevels.Count);

        for (int level = 0; level < 5; level++)
        {
            int expected = Math.Max(1, 32 >> level);
            string message = $"DDS mip {level} is {final.MipLevels[level].Width}x{final.MipLevels[level].Height}; "
                             + $"{expected}x{expected} was expected.";
            Require(final.MipLevels[level].Width == expected && final.MipLevels[level].Height == expected, message);
        }

        workspace.MarkSuccess();
    }

    /// <summary>
    ///     KTX v1's <c>numberOfMipmapLevels = 0</c> means exactly one level; the encoder must always write a
    ///     real count, and the decoder must materialize one level for a zero count.
    /// </summary>
    [Fact]
    public void Ktx1ZeroMipCountMeansOneLevel()
    {
        using Texture original = Build(TexelFormat.Bc7_UNorm, 16, 16);

        byte[] encoded = EncodeToBytes(original, KtxFormat.Instance);
        Assert.Equal(1u, ReadUInt32(encoded, 56));

        BinaryPrimitives.WriteUInt32LittleEndian(encoded.AsSpan(56), 0);

        using MemoryStream stream = new(encoded);
        using Texture decoded = Texture.Load(stream);
        Assert.Equal(1, decoded.MipLevels.Count);
        AssertSameTexture(original, decoded, TexelFormat.Bc7_UNorm, "KTX (numberOfMipmapLevels = 0)");

        byte[] reencoded = EncodeToBytes(decoded, KtxFormat.Instance);
        Assert.Equal(1u, ReadUInt32(reencoded, 56));
    }

    /// <summary>
    ///     Key/value data is container specific and is not carried into DDS, but it must not disturb the pixels:
    ///     a KTX2 texture with key/value data must decode to the same pixels after a DDS hop.
    /// </summary>
    [Fact]
    public void Ktx2KeyValueDataDoesNotAffectThePixelsAfterADdsHop()
    {
        using ParityWorkspace workspace = ParityWorkspace.Create("cross-kvd");
        using Texture original = Build(TexelFormat.R8G8B8A8_UNorm, Width, Height);

        byte[] keyValueData = CreateKeyValueBlock("KTXwriter", "cross-container"u8.ToArray());
        original.Metadata.GetFormatMetadata(Ktx2Format.Instance).KeyValueData = keyValueData;

        string ktxPath = workspace.PathFor("with-kvd.ktx2");
        original.Save(ktxPath);

        using Texture fromKtx2 = Texture.Load(ktxPath);
        Ktx2TextureMetadata metadata = fromKtx2.Metadata.GetFormatMetadata(Ktx2Format.Instance);
        Assert.Equal(keyValueData, metadata.KeyValueData);
        Assert.Single(metadata.KeyValuePairs);
        Assert.Equal("KTXwriter", metadata.KeyValuePairs[0].Key);

        byte[] expectedPixels = DecodePixels(fromKtx2.MipLevels[0].Surfaces[0]);

        using Texture fromDds = SaveAndLoad(fromKtx2, workspace.PathFor("hop.dds"));
        AssertSameTexture(original, fromDds, TexelFormat.R8G8B8A8_UNorm, "DDS");

        byte[] actualPixels = DecodePixels(fromDds.MipLevels[0].Surfaces[0]);
        Assert.Equal(Convert.ToHexString(expectedPixels), Convert.ToHexString(actualPixels));

        workspace.MarkSuccess();
    }

    private static Texture Build(
        TexelFormat texelFormat,
        int width,
        int height,
        int mipLevelCount = 1,
        int layerCount = 1,
        int faceCount = 1,
        int depth = 1,
        bool declareCubeMap = false)
    {
        Texture texture = new(TextureConfiguration.Default, new TextureMetadata());
        int surfaceIndex = 0;

        for (int level = 0; level < mipLevelCount; level++)
        {
            int levelWidth = Math.Max(1, width >> level);
            int levelHeight = Math.Max(1, height >> level);
            int levelDepth = Math.Max(1, depth >> level);
            TextureMipLevel mipLevel = texture.AddMipLevel(levelWidth, levelHeight, levelDepth);

            for (int layer = 0; layer < layerCount; layer++)
            {
                for (int face = 0; face < faceCount; face++)
                {
                    for (int slice = 0; slice < levelDepth; slice++)
                    {
                        TextureSurface surface = mipLevel.CreateSurface(texelFormat);
                        FillPayload(surface.GetPayload(), surfaceIndex);
                        surfaceIndex++;
                    }
                }
            }
        }

        if (declareCubeMap)
        {
            texture.Metadata.GetFormatMetadata(DdsFormat.Instance).ResourceMiscFlags |=
                D3d10ResourceMiscFlags.TextureCube;
        }

        return texture;
    }

    private static void FillPayload(Span<byte> payload, int surfaceIndex)
    {
        // A ramp that depends on both the surface index and the byte offset catches a swapped face, an
        // off-by-one block and a truncated payload.
        for (int i = 0; i < payload.Length; i++)
        {
            payload[i] = (byte)((surfaceIndex * 37) + (i * 11) + 5);
        }
    }

    private static Texture SaveAndLoad(Texture texture, string path)
    {
        texture.Save(path);
        return Texture.Load(path);
    }

    private static byte[] EncodeToBytes(Texture texture, ITextureFormat format)
    {
        using MemoryStream stream = new();
        texture.Save(stream, format);
        return stream.ToArray();
    }

    private static void AssertSameTexture(Texture expected, Texture actual, TexelFormat texelFormat, string container)
    {
        Require(
            expected.MipLevels.Count == actual.MipLevels.Count,
            $"{texelFormat}: {container} has {actual.MipLevels.Count} mip levels; {expected.MipLevels.Count} were expected.");

        for (int level = 0; level < expected.MipLevels.Count; level++)
        {
            TextureMipLevel expectedLevel = expected.MipLevels[level];
            TextureMipLevel actualLevel = actual.MipLevels[level];

            string dimensions = $"{texelFormat}: {container} mip {level} is "
                                + $"{actualLevel.Width}x{actualLevel.Height}x{actualLevel.Depth}; "
                                + $"{expectedLevel.Width}x{expectedLevel.Height}x{expectedLevel.Depth} was expected.";
            Require(
                expectedLevel.Width == actualLevel.Width
                && expectedLevel.Height == actualLevel.Height
                && expectedLevel.Depth == actualLevel.Depth,
                dimensions);

            string surfaces = $"{texelFormat}: {container} mip {level} has {actualLevel.Surfaces.Count} surfaces; "
                              + $"{expectedLevel.Surfaces.Count} were expected.";
            Require(expectedLevel.Surfaces.Count == actualLevel.Surfaces.Count, surfaces);

            for (int surface = 0; surface < expectedLevel.Surfaces.Count; surface++)
            {
                AssertPayload(
                    expectedLevel.Surfaces[surface].GetPayload().ToArray(),
                    actualLevel.Surfaces[surface].GetPayload().ToArray(),
                    texelFormat,
                    container,
                    level,
                    surface);
            }
        }
    }

    private static void AssertPayload(
        byte[] expected,
        byte[] actual,
        TexelFormat texelFormat,
        string container,
        int mip,
        int surface)
    {
        int length = Math.Min(expected.Length, actual.Length);
        int offset = -1;

        for (int i = 0; i < length; i++)
        {
            if (expected[i] != actual[i])
            {
                offset = i;
                break;
            }
        }

        if (offset < 0 && expected.Length == actual.Length)
        {
            return;
        }

        string detail = offset >= 0
            ? $"first differing byte at offset {offset}: expected 0x{expected[offset]:X2}, actual 0x{actual[offset]:X2}"
            : "the payload lengths differ";

        string message = $"{texelFormat}: {container} mip {mip} surface {surface}: {detail} "
                         + $"(expected {expected.Length} bytes, actual {actual.Length} bytes).";
        Require(false, message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            Assert.Fail(message);
        }
    }

    private static byte[] DecodePixels(TextureSurface surface)
    {
        using Image<Rgba32> image = surface.ToImage<Rgba32>();
        byte[] bytes = new byte[image.Width * image.Height * 4];
        image.CopyPixelDataTo(bytes);
        return bytes;
    }

    private static uint ReadUInt32(byte[] data, int offset)
        => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset));

    private static byte[] CreateKeyValueBlock(string key, byte[] value)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        int length = keyBytes.Length + 1 + value.Length;
        int padding = (4 - (length % 4)) % 4;
        byte[] block = new byte[4 + length + padding];

        BinaryPrimitives.WriteUInt32LittleEndian(block, (uint)length);
        keyBytes.CopyTo(block, 4);
        block[4 + keyBytes.Length] = 0;
        value.CopyTo(block, 5 + keyBytes.Length);
        return block;
    }
}
