using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ImageSharp.Textures.Formats.Dds;
using ImageSharp.Textures.Formats.Dds.Enums;
using ImageSharp.Textures.Parity.Tests.TestUtilities;
using Xunit.Abstractions;

namespace ImageSharp.Textures.Parity.Tests;

/// <summary>
///     Container interoperability against texconv: it must read every DDS we write, report the level, layer and
///     face counts we wrote, and decode the same pixels we decode.
/// </summary>
/// <remarks>
///     <para>
///         texconv's verbose read line is produced by <c>PrintInfo</c> in DirectXTex's texconv.cpp and has the shape
///         <c>(WxH[,mips][,arraySize] FORMAT DIMENSION)</c>, omitting mips and the array size when they are one. A real
///         sample from this machine, reading our four level BC1 dump:
///         <c>reading ours.dds (64x64,4 BC1_UNORM 2D) as (64x64 R8G8B8A8_UNORM 2D a:Opaque)</c>.
///         Cubemaps report <c>Cube</c> (or <c>CubeArray</c>) with the face count folded into the array size, so a cube
///         prints as <c>(64x64,6 BC1_UNORM Cube)</c>.
///     </para>
///     <para>
///         texconv does not create the <c>-o</c> directory, so every dump directory is created before the run.
///     </para>
/// </remarks>
[Trait("Category", "ContainerInterop")]
public partial class ContainerInteropTests : IClassFixture<ContainerInteropSummary>
{
    private const int BaseWidth = 64;
    private const int BaseHeight = 64;

    private static readonly string[] Formats =
    [
        "BC1_UNORM",
        "BC4_UNORM",
        "BC5_UNORM",
        "BC7_UNORM",
        "R8G8B8A8_UNORM"
    ];

    private static readonly Shape[] Shapes =
    [
        new("single-1mip", 1, 1, false),
        new("single-4mip", 4, 1, false),
        new("array2-1mip", 1, 2, false),
        new("array2-4mip", 4, 2, false),
        new("cube-1mip", 1, 1, true),
        new("cube-4mip", 4, 1, true)
    ];

    private readonly ITestOutputHelper output;
    private readonly ContainerInteropSummary summary;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ContainerInteropTests" /> class.
    /// </summary>
    /// <param name="output">The test output.</param>
    /// <param name="summary">The class fixture collecting the container findings.</param>
    public ContainerInteropTests(ITestOutputHelper output, ContainerInteropSummary summary)
    {
        this.output = output;
        this.summary = summary;
    }

    /// <summary>
    ///     Gets the format and container shape pairs.
    /// </summary>
    public static TheoryData<string, string> ShapeCases
    {
        get
        {
            TheoryData<string, string> cases = new();

            foreach (string format in Formats)
            {
                foreach (Shape shape in Shapes)
                {
                    cases.Add(format, shape.Name);
                }
            }

            return cases;
        }
    }

    /// <summary>
    ///     texconv must read every DDS we write, for every format and container shape.
    /// </summary>
    /// <param name="format">The texconv format spelling.</param>
    /// <param name="shapeName">The container shape name.</param>
    [TexconvTheory]
    [MemberData(nameof(ShapeCases))]
    public void TexconvReadsEveryDdsWeWrite(string format, string shapeName)
    {
        Shape shape = Shapes.Single(candidate => candidate.Name == shapeName);
        using ParityWorkspace workspace = ParityWorkspace.Create($"interop-{format}-{shapeName}");

        string ddsPath = workspace.PathFor("ours.dds");
        using (Texture texture = BuildTexture(shape))
        {
            Encode(texture, format, ddsPath, shape.Layers > 1);
        }

        Directory.CreateDirectory(workspace.PathFor("dump"));
        string text = TexconvProcess.Run(workspace, "-ft", "tga", "-f", "R8G8B8A8_UNORM", "-o", "dump", "--", ddsPath);

        Assert.Contains("reading", text, StringComparison.Ordinal);
        this.output.WriteLine($"{format} {shapeName}: texconv read {new FileInfo(ddsPath).Length} bytes");
        workspace.MarkSuccess();
    }

    /// <summary>
    ///     texconv must report the mip level, array layer and cube face counts we wrote.
    /// </summary>
    [TexconvFact]
    public void TexconvReportsTheLevelsLayersAndFacesWeWrote()
    {
        using ParityWorkspace workspace = ParityWorkspace.Create("interop-levels");

        foreach (Shape shape in Shapes)
        {
            string ddsPath = workspace.PathFor($"{shape.Name}.dds");
            using (Texture texture = BuildTexture(shape))
            {
                Encode(texture, "BC1_UNORM", ddsPath, shape.Layers > 1);
            }

            Directory.CreateDirectory(workspace.PathFor("dump"));
            string text = TexconvProcess.Run(workspace, "-ft", "tga", "-f", "R8G8B8A8_UNORM", "-o", "dump", "--",
                ddsPath);

            Match match = ReadLine().Match(text);
            Assert.True(match.Success, $"{shape.Name}: no read line in:{Environment.NewLine}{text}");

            int width = int.Parse(match.Groups["w"].Value, CultureInfo.InvariantCulture);
            int height = int.Parse(match.Groups["h"].Value, CultureInfo.InvariantCulture);
            string dimension = match.Groups["dim"].Value;

            // PrintInfo writes the mip count first (only when it exceeds one), then the array size (only when
            // it exceeds one); the face count of a cube is folded into the array size.
            List<int> reported = match.Groups["extra"].Value
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(value => int.Parse(value, CultureInfo.InvariantCulture))
                .ToList();

            int surfaces = shape.Cube ? 6 : shape.Layers;
            List<int> expected = new();
            if (shape.MipCount > 1)
            {
                expected.Add(shape.MipCount);
            }

            if (surfaces > 1)
            {
                expected.Add(surfaces);
            }

            Assert.Equal(expected, reported);
            Assert.Equal(BaseWidth, width);
            Assert.Equal(BaseHeight, height);
            Assert.Equal(shape.Cube ? "Cube" : shape.Layers > 1 ? "2DArray" : "2D", dimension);

            this.output.WriteLine($"{shape.Name}: {match.Value}");
            this.summary.Add($"- `{shape.Name}`: texconv reported `{match.Value}`");
        }

        workspace.MarkSuccess();
    }

    /// <summary>
    ///     Our default DDS header must equal the one texconv writes for the same image and format.
    /// </summary>
    /// <remarks>
    ///     The theory covers the four block-compressed formats. R8G8B8A8_UNORM is deliberately excluded: texconv
    ///     writes a legacy masked A8R8G8B8 pixel format for uncompressed input while the encoder always uses a DX10
    ///     header for R8G8B8A8, so those two headers differ by construction even though dwFlags match. That format
    ///     is covered by the read, level and round-trip cases instead.
    /// </remarks>
    /// <param name="format">The texconv format spelling.</param>
    [TexconvTheory]
    [InlineData("BC1_UNORM")]
    [InlineData("BC4_UNORM")]
    [InlineData("BC5_UNORM")]
    [InlineData("BC7_UNORM")]
    public void HeaderBytesMatchTexconv(string format)
    {
        using ParityWorkspace workspace = ParityWorkspace.Create($"interop-header-{format}");

        using Image<Rgba32> source = Render(BaseWidth, BaseHeight, 0, 0);
        string sourcePath = workspace.PathFor("source.tga");
        source.Save(sourcePath);

        Directory.CreateDirectory(workspace.PathFor("reference"));
        TexconvProcess.Run(workspace, "-ft", "dds", "-f", format, "-m", "1", "-o", "reference", "--", sourcePath);
        string referencePath = workspace.PathFor(Path.Combine("reference", "source.dds"));

        string oursPath = workspace.PathFor("ours.dds");
        using (Texture texture = BuildTexture(new Shape("single-1mip", 1, 1, false), source))
        {
            Encode(texture, format, oursPath);
        }

        byte[] ours = File.ReadAllBytes(oursPath);
        byte[] reference = File.ReadAllBytes(referencePath);
        int headerLength = FourCc(ours) == "DX10" ? 148 : 128;
        Assert.Equal(headerLength, FourCc(reference) == "DX10" ? 148 : 128);

        bool headerMatches = ours.AsSpan(0, headerLength).SequenceEqual(reference.AsSpan(0, headerLength));
        int payloadLength = Math.Min(ours.Length, reference.Length) - headerLength;
        int identical = 0;
        for (int i = 0; i < payloadLength; i++)
        {
            if (ours[headerLength + i] == reference[headerLength + i])
            {
                identical++;
            }
        }

        double payloadPercent = payloadLength <= 0 ? 0 : 100.0 * identical / payloadLength;
        this.output.WriteLine(
            $"{format}: header {headerLength} bytes {(headerMatches ? "identical" : "DIFFERS")}; payload {payloadPercent:0.0}% identical");

        string headerDump =
            $"{format}: the {headerLength}-byte header differs from texconv.{Environment.NewLine}" +
            $"ours:    {DescribeHeader(ours)}{Environment.NewLine}" +
            $"texconv: {DescribeHeader(reference)}";

        Assert.True(headerMatches, headerDump);

        // The encoded payload is byte-identical to texconv's for this image; a divergence is a real encoder
        // difference, so it is asserted rather than recorded.
        Assert.Equal(payloadLength, identical);

        this.summary.Add(
            $"- `{format}`: {headerLength}-byte header identical to texconv; payload {payloadPercent:0.0}% identical.");
        this.summary.Add($"  - `{DescribeHeader(ours)}`");

        this.output.WriteLine($"ours     {DescribeHeader(ours)}");
        this.output.WriteLine($"texconv  {DescribeHeader(reference)}");
        workspace.MarkSuccess();
    }

    /// <summary>
    ///     texconv's decode of our DDS must match ours within the plan's decode thresholds.
    /// </summary>
    /// <param name="format">The texconv format spelling.</param>
    [TexconvTheory]
    [InlineData("BC1_UNORM")]
    [InlineData("BC4_UNORM")]
    [InlineData("BC5_UNORM")]
    [InlineData("BC7_UNORM")]
    public void RoundTripThroughTexconvMatchesOurDecode(string format)
    {
        using ParityWorkspace workspace = ParityWorkspace.Create($"interop-roundtrip-{format}");

        string ddsPath = workspace.PathFor("ours.dds");
        ParityImage image = ParityCorpus.Get("gradient-256");

        using (Texture texture = BuildTextureFromImage(ParityCorpus.Get("gradient-256")))
        {
            Encode(texture, format, ddsPath);
        }

        Directory.CreateDirectory(workspace.PathFor("dump"));
        TexconvProcess.Run(workspace, "-ft", "tga", "-f", "R8G8B8A8_UNORM", "-o", "dump", "--", ddsPath);

        byte[] reference;
        using (Image<Rgba32> loaded = Image.Load<Rgba32>(workspace.PathFor(Path.Combine("dump", "ours.tga"))))
        {
            reference = CopyPixels(loaded);
        }

        byte[] ours;
        using (Texture texture = Texture.Load(ddsPath))
        {
            using Image<Rgba32> decoded = texture.MipLevels[0].Surfaces[0].ToImage<Rgba32>();
            ours = CopyPixels(decoded);
        }

        ParityChannels channels = format switch
        {
            "BC1_UNORM" => ParityChannels.Bc1,
            "BC4_UNORM" => ParityChannels.Bc4,
            "BC5_UNORM" => ParityChannels.Bc5,
            _ => ParityChannels.Bc7
        };

        ParityDiff diff = ParityMetrics.Compare(reference, ours, image.Width, image.Height, channels);
        int allowed = format == "BC7_UNORM" ? 0 : 1;

        this.output.WriteLine($"{format}: round-trip max {diff.MaxChannelDiff}, {diff.DifferingPixels} px differ");
        this.summary.Add(
            $"- `{format}` round-trip through texconv: max channel diff {diff.MaxChannelDiff}, {diff.DifferingPixels} of {image.Width * image.Height} pixels differ (allowed {allowed}).");

        Assert.True(
            diff.MaxChannelDiff <= allowed,
            $"{format}: texconv's decode of our DDS differs by {diff.MaxChannelDiff} (allowed {allowed}); source {image.Name}.");

        workspace.MarkSuccess();
    }

    [GeneratedRegex(
        @"\((?<w>\d+)x(?<h>\d+)(?<extra>(?:,\d+)*) (?<format>[A-Z0-9_]+) (?<dim>2DArray|CubeArray|2D|Cube|3D|1D)(?: a:\w+)?\)",
        RegexOptions.CultureInvariant)]
    private static partial Regex ReadLine();

    private static Texture BuildTextureFromImage(ParityImage image)
    {
        Texture texture = new(TextureConfiguration.Default, new TextureMetadata());
        TextureMipLevel mip = texture.AddMipLevel(image.Width, image.Height, 1);
        TextureSurface surface = mip.CreateSurface(TexelFormat.R8G8B8A8_UNorm);

        using Image<Rgba32> source = Image.Load<Rgba32>(image.Path);
        surface.CopyFrom(source);
        return texture;
    }

    private static Texture BuildTexture(Shape shape)
    {
        Texture texture = new(TextureConfiguration.Default, new TextureMetadata());

        for (int level = 0; level < shape.MipCount; level++)
        {
            int width = Math.Max(1, BaseWidth >> level);
            int height = Math.Max(1, BaseHeight >> level);
            TextureMipLevel mip = texture.AddMipLevel(width, height, 1);
            int surfaces = shape.Cube ? 6 : shape.Layers;

            for (int index = 0; index < surfaces; index++)
            {
                TextureSurface surface = mip.CreateSurface(TexelFormat.R8G8B8A8_UNorm);
                using Image<Rgba32> image = Render(width, height, level, index);
                surface.CopyFrom(image);
            }
        }

        if (shape.Cube)
        {
            texture.Metadata.GetFormatMetadata(DdsFormat.Instance).ResourceMiscFlags |=
                D3d10ResourceMiscFlags.TextureCube;
        }

        return texture;
    }

    private static Texture BuildTexture(Shape shape, Image<Rgba32> source)
    {
        Texture texture = new(TextureConfiguration.Default, new TextureMetadata());
        TextureMipLevel mip = texture.AddMipLevel(source.Width, source.Height, 1);
        TextureSurface surface = mip.CreateSurface(TexelFormat.R8G8B8A8_UNorm);
        surface.CopyFrom(source);
        return texture;
    }

    private static Image<Rgba32> Render(int width, int height, int level, int surface)
    {
        Image<Rgba32> image = new(width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                byte r = (byte)(x * 255 / Math.Max(1, width - 1));
                byte g = (byte)(y * 255 / Math.Max(1, height - 1));
                byte b = (byte)(((level * 37) + (surface * 53)) & 0xFF);
                image[x, y] = new Rgba32(r, g, b, 255);
            }
        }

        return image;
    }

    private static void Encode(Texture texture, string format, string path, bool useDx10 = false)
        => texture.Save(
            path,
            new DdsTextureEncoder(new DdsEncodingOptions
            {
                TargetTexelFormat = ParseFormat(format), UseDx10Header = useDx10
            }));

    private static TexelFormat ParseFormat(string format) => format switch
    {
        "BC1_UNORM" => TexelFormat.Bc1_UNorm,
        "BC4_UNORM" => TexelFormat.Bc4_UNorm,
        "BC5_UNORM" => TexelFormat.Bc5_UNorm,
        "BC7_UNORM" => TexelFormat.Bc7_UNorm,
        _ => TexelFormat.R8G8B8A8_UNorm
    };

    private static string FourCc(byte[] dds) => Encoding.ASCII.GetString(dds, 84, 4);

    private static string DescribeHeader(byte[] dds)
    {
        StringBuilder builder = new();
        builder.Append(CultureInfo.InvariantCulture, $"size={U32(dds, 4)} flags=0x{U32(dds, 8):X8} ");
        builder.Append(CultureInfo.InvariantCulture,
            $"{U32(dds, 16)}x{U32(dds, 12)} pitch={U32(dds, 20)} depth={U32(dds, 24)} mips={U32(dds, 28)} ");
        builder.Append(CultureInfo.InvariantCulture,
            $"pf(flags=0x{U32(dds, 80):X} fourcc={FourCc(dds)}) caps=0x{U32(dds, 108):X8} caps2=0x{U32(dds, 112):X8}");

        if (FourCc(dds) == "DX10")
        {
            builder.Append(CultureInfo.InvariantCulture,
                $" dxgi={U32(dds, 128)} dim={U32(dds, 132)} misc=0x{U32(dds, 136):X} array={U32(dds, 140)} misc2={U32(dds, 144)}");
        }

        return builder.ToString();
    }

    private static uint U32(byte[] data, int offset)
        => (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));

    private static byte[] CopyPixels(Image<Rgba32> image)
    {
        byte[] bytes = new byte[image.Width * image.Height * 4];
        image.CopyPixelDataTo(bytes);
        return bytes;
    }

    private readonly record struct Shape(string Name, int MipCount, int Layers, bool Cube);
}
