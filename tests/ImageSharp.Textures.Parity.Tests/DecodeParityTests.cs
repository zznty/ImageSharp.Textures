using System.Security.Cryptography;
using ImageSharp.Textures.Parity.Tests.TestUtilities;
using Xunit.Abstractions;

namespace ImageSharp.Textures.Parity.Tests;

/// <summary>
///     Decode parity against the pinned texconv executable.
/// </summary>
/// <remarks>
///     Every case encodes a corpus TGA with texconv, decodes the resulting DDS back to an RGBA TGA with texconv,
///     decodes the same DDS with the library and compares the two. The gates are the plan §2.6 numbers: BC7 is
///     bit-exact and BC1/BC4/BC5 stay within one least significant bit. BC4 and BC5 are compared on their declared
///     channels only because texconv converts BC4 through its single-channel R8 surface and splats red across RGB;
///     our green, blue and alpha values are asserted separately instead.
/// </remarks>
[Trait("Category", "ParityDecode")]
public class DecodeParityTests : IClassFixture<DecodeParitySummary>
{
    private readonly ITestOutputHelper output;
    private readonly DecodeParitySummary summary;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DecodeParityTests" /> class.
    /// </summary>
    /// <param name="output">The test output.</param>
    /// <param name="summary">The class fixture collecting the parity table.</param>
    public DecodeParityTests(ITestOutputHelper output, DecodeParitySummary summary)
    {
        this.output = output;
        this.summary = summary;
    }

    /// <summary>
    ///     Gets the format and corpus image pairs. The 1024 image runs for BC1 and BC7 only to keep the run time sane.
    /// </summary>
    public static TheoryData<string, string> Cases { get; } = BuildCases();

    /// <summary>
    ///     Decodes every supported block format and compares it with texconv.
    /// </summary>
    /// <param name="format">The texconv format spelling.</param>
    /// <param name="imageName">The corpus image name.</param>
    [TexconvTheory]
    [MemberData(nameof(Cases))]
    public void DecodeMatchesTexconv(string format, string imageName)
    {
        ParityImage image = ParityCorpus.Get(imageName);
        using ParityWorkspace workspace = ParityWorkspace.Create($"decode-{format}-{imageName}");

        (byte[] ours, byte[] reference) = DecodeBoth(format, image, workspace);

        if (format is "BC4_UNORM" or "BC5_UNORM")
        {
            AssertDeclaredChannels(format, ours);
        }

        ParityChannels channels = ChannelsFor(format);
        int maxAllowedDiff = format == "BC7_UNORM" ? 0 : 1;
        ParityDiff diff = ParityMetrics.AssertDecodeParity(
            format,
            image,
            reference,
            ours,
            channels,
            maxAllowedDiff,
            workspace,
            this.output);

        this.summary.Add(format, image, diff);
        workspace.MarkSuccess();
    }

    /// <summary>
    ///     The flat corpus image decodes to the same colour everywhere, which proves the pipeline before any
    ///     orientation question is asked.
    /// </summary>
    /// <param name="format">The texconv format spelling.</param>
    [TexconvTheory]
    [InlineData("BC1_UNORM")]
    [InlineData("BC4_UNORM")]
    [InlineData("BC5_UNORM")]
    [InlineData("BC7_UNORM")]
    public void FlatCorpusDecodesUniformly(string format)
    {
        ParityImage image = ParityCorpus.Get("flat-4x4");
        using ParityWorkspace workspace = ParityWorkspace.Create($"flat-{format}");

        (byte[] ours, byte[] reference) = DecodeBoth(format, image, workspace);

        Rgba32 first = ReadPixel(ours, 0);
        for (int i = 1; i < image.Width * image.Height; i++)
        {
            Assert.Equal(first, ReadPixel(ours, i));
        }

        this.output.WriteLine($"{format}: flat colour decodes to {first}");

        // BC1 can only reach the 565 grid; the other formats are exact for this colour.
        int tolerance = format == "BC1_UNORM" ? 8 : 0;
        ParityDiff diff = ParityMetrics.Compare(
            reference,
            ours,
            image.Width,
            image.Height,
            ChannelsFor(format));
        Assert.True(
            diff.MaxChannelDiff <= tolerance,
            $"{format}: flat colour differs from the texconv reference by {diff.MaxChannelDiff} (tolerance {tolerance}).");

        workspace.MarkSuccess();
    }

    /// <summary>
    ///     Proves the TGA row order of a texconv dump matches the DDS row order, so the 4096-pixel comparisons are
    ///     comparing the same texels.
    /// </summary>
    [TexconvFact]
    public void TgaDumpRowsFollowTheSourceOrientation()
    {
        using ParityWorkspace workspace = ParityWorkspace.Create("row-order");

        // Four rows of red above four of blue: BC7 encodes each 4x4 block losslessly, so the colours are exact.
        using Image<Rgba32> source = new(8, 8);
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                source[x, y] = y < 4 ? new Rgba32(32, 0, 0, 255) : new Rgba32(0, 0, 32, 255);
            }
        }

        string sourcePath = workspace.PathFor("rows.tga");
        source.Save(sourcePath);

        Directory.CreateDirectory(workspace.PathFor("dds"));
        TexconvProcess.Run(workspace, "-ft", "dds", "-f", "BC7_UNORM", "-o", "dds", "--", sourcePath);
        string ddsPath = workspace.PathFor(Path.Combine("dds", "rows.dds"));

        Directory.CreateDirectory(workspace.PathFor("tga"));
        TexconvProcess.Run(workspace, "-ft", "tga", "-f", "R8G8B8A8_UNORM", "-o", "tga", "--", ddsPath);
        string referencePath = workspace.PathFor(Path.Combine("tga", "rows.tga"));

        using Image<Rgba32> reference = Image.Load<Rgba32>(referencePath);
        Assert.Equal(8, reference.Width);
        Assert.Equal(8, reference.Height);

        // The dump's first row is the source's first row.
        Assert.Equal(new Rgba32(32, 0, 0, 255), reference[0, 0]);
        Assert.Equal(new Rgba32(32, 0, 0, 255), reference[7, 0]);
        Assert.Equal(new Rgba32(0, 0, 32, 255), reference[0, 7]);
        Assert.Equal(new Rgba32(0, 0, 32, 255), reference[7, 7]);

        // And our DDS decode uses the same row order as the dump.
        using Texture texture = Texture.Load(ddsPath);
        using Image<Rgba32> decoded = texture.MipLevels[0].Surfaces[0].ToImage<Rgba32>();

        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                Assert.Equal(reference[x, y], decoded[x, y]);
            }
        }

        this.output.WriteLine("TGA row order: texconv dump rows 0..7 match the source rows and our DDS decode.");
        workspace.MarkSuccess();
    }

    /// <summary>
    ///     Writing a corpus image twice produces the same bytes, so the cache key is stable.
    /// </summary>
    [Fact]
    public void CorpusGenerationIsDeterministic()
    {
        foreach (ParityImage image in ParityCorpus.All)
        {
            byte[] regenerated = ParityCorpus.Encode(image.Name);
            byte[] cached = File.ReadAllBytes(image.Path);

            Assert.Equal(cached, regenerated);
            Assert.Equal(image.Hash, Convert.ToHexStringLower(SHA256.HashData(regenerated)));
        }
    }

    private static (byte[] Ours, byte[] Reference) DecodeBoth(string format, ParityImage image,
        ParityWorkspace workspace)
    {
        string sourceName = Path.GetFileName(image.Path);

        Directory.CreateDirectory(workspace.PathFor("dds"));
        TexconvProcess.Run(workspace, "-ft", "dds", "-f", format, "-o", "dds", "--", image.Path);
        string ddsPath = workspace.PathFor(Path.Combine("dds", Path.ChangeExtension(sourceName, ".dds")));

        Directory.CreateDirectory(workspace.PathFor("tga"));
        TexconvProcess.Run(workspace, "-ft", "tga", "-f", "R8G8B8A8_UNORM", "-o", "tga", "--", ddsPath);
        string referencePath = workspace.PathFor(Path.Combine("tga", Path.ChangeExtension(sourceName, ".tga")));

        byte[] ours;
        using (Texture texture = Texture.Load(ddsPath))
        {
            TextureSurface surface = texture.MipLevels[0].Surfaces[0];
            Assert.Equal(image.Width, surface.Width);
            Assert.Equal(image.Height, surface.Height);

            using Image<Rgba32> decoded = surface.ToImage<Rgba32>();
            ours = CopyPixels(decoded);
        }

        byte[] reference;
        using (Image<Rgba32> loaded = Image.Load<Rgba32>(referencePath))
        {
            Assert.Equal(image.Width, loaded.Width);
            Assert.Equal(image.Height, loaded.Height);
            reference = CopyPixels(loaded);
        }

        return (ours, reference);
    }

    private static ParityChannels ChannelsFor(string format) => format switch
    {
        "BC1_UNORM" => ParityChannels.Bc1,
        "BC4_UNORM" => ParityChannels.Bc4,
        "BC5_UNORM" => ParityChannels.Bc5,
        _ => ParityChannels.Bc7
    };

    private static void AssertDeclaredChannels(string format, byte[] rgba)
    {
        for (int i = 0; i < rgba.Length; i += 4)
        {
            if (format == "BC4_UNORM")
            {
                Assert.True(rgba[i + 1] == 0, $"BC4 texel {i / 4}: green is {rgba[i + 1]}, expected 0.");
                Assert.True(rgba[i + 2] == 0, $"BC4 texel {i / 4}: blue is {rgba[i + 2]}, expected 0.");
            }
            else
            {
                Assert.True(rgba[i + 2] == 0, $"BC5 texel {i / 4}: blue is {rgba[i + 2]}, expected 0.");
            }

            Assert.True(rgba[i + 3] == 255, $"{format} texel {i / 4}: alpha is {rgba[i + 3]}, expected 255.");
        }
    }

    private static Rgba32 ReadPixel(byte[] rgba, int index)
    {
        int offset = index * 4;
        return new Rgba32(rgba[offset], rgba[offset + 1], rgba[offset + 2], rgba[offset + 3]);
    }

    private static byte[] CopyPixels(Image<Rgba32> image)
    {
        byte[] bytes = new byte[image.Width * image.Height * 4];
        image.CopyPixelDataTo(bytes);
        return bytes;
    }

    private static TheoryData<string, string> BuildCases()
    {
        TheoryData<string, string> cases = new();
        string[] formats = ["BC1_UNORM", "BC4_UNORM", "BC5_UNORM", "BC7_UNORM"];

        foreach (string format in formats)
        {
            foreach (ParityImage image in ParityCorpus.All)
            {
                if (image.Name == "gradient-1024" && format is not ("BC1_UNORM" or "BC7_UNORM"))
                {
                    continue;
                }

                cases.Add(format, image.Name);
            }
        }

        return cases;
    }
}
