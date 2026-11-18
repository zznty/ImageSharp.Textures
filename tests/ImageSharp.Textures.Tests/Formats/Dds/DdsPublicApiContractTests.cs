using System.Buffers.Binary;
using ImageSharp.Textures.Formats.Dds;
using ImageSharp.Textures.Tests.TestUtilities;

namespace ImageSharp.Textures.Tests.Formats.Dds;

/// <summary>
///     Public contract checks for the DDS container that are not covered by the container interop suite.
/// </summary>
[Trait("Category", "DdsEncode")]
public class DdsPublicApiContractTests
{
    [Fact]
    public void SaveDoesNotRecompressBlockCompressedPayloads()
    {
        using Texture texture = TestTextureFactory.CreateTexture(
            TextureConfiguration.Default,
            8,
            8,
            texelFormat: TexelFormat.Bc7_UNorm);

        TextureSurface surface = texture.MipLevels[0].Surfaces[0];
        Span<byte> payload = surface.GetPayload();
        for (int i = 0; i < payload.Length; i++)
        {
            payload[i] = (byte)((i * 7) + 3);
        }

        byte[] expected = payload.ToArray();
        using MemoryStream stream = new();

        texture.Save(stream, DdsFormat.Instance);

        byte[] file = stream.ToArray();
        Assert.Equal(98u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(128)));

        using MemoryStream reloaded = new(file);
        using Texture decoded = Texture.Load(reloaded, new DdsTextureDecoder());
        Assert.Equal(TexelFormat.Bc7_UNorm, decoded.MipLevels[0].Surfaces[0].TexelFormat);
        Assert.Equal(expected, decoded.MipLevels[0].Surfaces[0].GetPayload().ToArray());
    }
}
