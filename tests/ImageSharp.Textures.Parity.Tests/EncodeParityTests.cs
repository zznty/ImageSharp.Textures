using System.Globalization;
using ImageSharp.Textures.Formats.Dds;
using ImageSharp.Textures.Parity.Tests.TestUtilities;
using Xunit.Abstractions;

namespace ImageSharp.Textures.Parity.Tests;

/// <summary>
///     Encode quality parity against the pinned texconv executable.
/// </summary>
/// <remarks>
///     <para>
///         Every case encodes a corpus image twice: once with our <see cref="DdsTextureEncoder" /> (converting an
///         uncompressed surface to the block format through <see cref="DdsEncodingOptions.TargetTexelFormat" />) and once
///         with texconv from the same TGA. Both payloads are decoded by texconv to <c>R8G8B8A8_UNORM</c> and compared
///         with the source image, so the gate is decoded-image quality rather than payload identity: a legitimate
///         near-tie in the endpoint search may pick a different but equally good block, which payload comparison would
///         flag as a failure while the decoded images are indistinguishable.
///     </para>
///     <para>
///         The gates are the plan §6 Phase 9 numbers: per image <c>PSNR(ours) &gt;= PSNR(texconv) - 0.5 dB</c>, corpus
///         <c>&gt;= corpus(texconv) - 0.2 dB</c>, and for BC7 <c>corpus(default) &gt;= corpus(quick) + 0.2 dB</c>. PSNR is
///         measured over each format's declared colour channels, matching the decode parity suite. The corpus figure is
///         error weighted (the PSNR of the summed squared error) because most corpus images encode perfectly: an
///         arithmetic mean of per-image PSNR would be infinite and the gate vacuous.
///     </para>
/// </remarks>
[Trait("Category", "EncodeParity")]
public class EncodeParityTests : IClassFixture<EncodeParitySummary>
{
    private const double PerImageMarginDb = 0.5;
    private const double CorpusMeanMarginDb = 0.2;
    private const double DefaultOverQuickMarginDb = 0.2;

    private readonly ITestOutputHelper output;
    private readonly EncodeParitySummary summary;

    /// <summary>
    ///     Initializes a new instance of the <see cref="EncodeParityTests" /> class.
    /// </summary>
    /// <param name="output">The test output.</param>
    /// <param name="summary">The class fixture collecting the parity table.</param>
    public EncodeParityTests(ITestOutputHelper output, EncodeParitySummary summary)
    {
        this.output = output;
        this.summary = summary;
    }

    /// <summary>
    ///     Encodes every corpus image with our encoder and with texconv, decodes both with texconv and compares the
    ///     quality against the source.
    /// </summary>
    /// <param name="format">The texconv format spelling.</param>
    [TexconvTheory]
    [InlineData("BC1_UNORM")]
    [InlineData("BC4_UNORM")]
    [InlineData("BC5_UNORM")]
    [InlineData("BC7_UNORM")]
    public void EncodeQualityMatchesTexconv(string format)
    {
        bool isBc7 = format == "BC7_UNORM";
        ParityChannels channels = ChannelsFor(format);
        string channelName = ChannelName(format);
        int sourceDiffLimit = format is "BC4_UNORM" or "BC5_UNORM" ? 8 : 64;
        List<Measurement> measured = new();
        List<string> failures = new();

        foreach (ParityImage image in ParityCorpus.All)
        {
            using ParityWorkspace workspace = ParityWorkspace.Create($"encode-{format}-{image.Name}");

            using Image<Rgba32> source = Image.Load<Rgba32>(image.Path);
            Assert.Equal(image.Width, source.Width);
            Assert.Equal(image.Height, source.Height);
            byte[] sourcePixels = CopyPixels(source);

            string oursPath = workspace.PathFor("ours.dds");
            EncodeOurs(source, format, oursPath, Bc7EncodingQuality.Default);
            byte[] ours = DecodeWithTexconv(workspace, oursPath, "decode-ours");

            byte[] reference = EncodeAndDecodeWithTexconv(workspace, image, format, false, "reference");

            if (format is "BC4_UNORM" or "BC5_UNORM")
            {
                // Asserted on our own decoder: texconv dumps BC4 through a single channel surface and splats
                // red across RGB, so its dump cannot show the declared channel semantics.
                AssertDeclaredChannels(format, DecodeOurs(oursPath), image);
            }

            Quality oursQuality = Measure(sourcePixels, ours, image, channels);
            Quality referenceQuality = Measure(sourcePixels, reference, image, channels);

            Quality quickOurs = default;
            Quality quickReference = default;
            if (isBc7)
            {
                string quickPath = workspace.PathFor("ours-quick.dds");
                EncodeOurs(source, format, quickPath, Bc7EncodingQuality.Quick);
                byte[] quickOursPixels = DecodeWithTexconv(workspace, quickPath, "decode-ours-quick");
                byte[] quickReferencePixels =
                    EncodeAndDecodeWithTexconv(workspace, image, format, true, "reference-quick");

                quickOurs = Measure(sourcePixels, quickOursPixels, image, channels);
                quickReference = Measure(sourcePixels, quickReferencePixels, image, channels);
            }

            string row = string.Create(
                CultureInfo.InvariantCulture,
                $"{format,-10} {image.Name,-20} {image.Width,4}x{image.Height,-4} {channelName,-3} ours {ParityMetrics.FormatPsnr(oursQuality.Psnr),7} texconv {ParityMetrics.FormatPsnr(referenceQuality.Psnr),7} quick ours {ParityMetrics.FormatPsnr(quickOurs.Psnr),7} quick texconv {ParityMetrics.FormatPsnr(quickReference.Psnr),7} max source diff {oursQuality.MaxChannelDiff,3}");

            this.output.WriteLine(row);

            this.summary.Add(
                format,
                image,
                channelName,
                oursQuality.Psnr,
                referenceQuality.Psnr,
                isBc7 ? quickOurs.Psnr : double.NaN,
                isBc7 ? quickReference.Psnr : double.NaN,
                oursQuality.MaxChannelDiff);

            if (image.Name.StartsWith("gradient-", StringComparison.Ordinal) &&
                oursQuality.MaxChannelDiff > sourceDiffLimit)
            {
                string sanityMessage = string.Create(
                    CultureInfo.InvariantCulture,
                    $"{format} '{image.Name}' ({image.Width}x{image.Height}): decoded channel diff {oursQuality.MaxChannelDiff} exceeds {sourceDiffLimit} LSB (PSNR {ParityMetrics.FormatPsnr(oursQuality.Psnr)}).");

                failures.Add(sanityMessage);
            }

            if (oursQuality.Psnr < referenceQuality.Psnr - PerImageMarginDb)
            {
                string perImageMessage = string.Create(
                    CultureInfo.InvariantCulture,
                    $"{format} '{image.Name}' ({image.Width}x{image.Height}, {channelName}): our PSNR {ParityMetrics.FormatPsnr(oursQuality.Psnr)} is more than {PerImageMarginDb} dB below texconv's {ParityMetrics.FormatPsnr(referenceQuality.Psnr)}.");

                failures.Add(perImageMessage);
            }

            if (isBc7 && quickOurs.Psnr < quickReference.Psnr - PerImageMarginDb)
            {
                string quickMessage = string.Create(
                    CultureInfo.InvariantCulture,
                    $"BC7_UNORM (quick) '{image.Name}' ({image.Width}x{image.Height}, {channelName}): our PSNR {ParityMetrics.FormatPsnr(quickOurs.Psnr)} is more than {PerImageMarginDb} dB below texconv's {ParityMetrics.FormatPsnr(quickReference.Psnr)}.");

                failures.Add(quickMessage);
            }

            measured.Add(new Measurement(image, oursQuality, referenceQuality, quickOurs, quickReference));
            workspace.MarkSuccess();
        }

        double corpusOurs = CorpusPsnr(measured.Select(entry => entry.Ours));
        double corpusReference = CorpusPsnr(measured.Select(entry => entry.Reference));
        double minMargin = Minimum(measured.Select(entry => entry.Ours.Psnr - entry.Reference.Psnr));

        this.output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{format} corpus PSNR: ours {ParityMetrics.FormatPsnr(corpusOurs)} texconv {ParityMetrics.FormatPsnr(corpusReference)} min per-image margin {FormatMargin(minMargin)} dB"));

        if (failures.Count > 0)
        {
            Assert.Fail(string.Join(Environment.NewLine, failures));
        }

        string meanMessage = string.Create(
            CultureInfo.InvariantCulture,
            $"{format} corpus PSNR {ParityMetrics.FormatPsnr(corpusOurs)} is more than {CorpusMeanMarginDb} dB below texconv's {ParityMetrics.FormatPsnr(corpusReference)} over {measured.Count} images.");

        Assert.True(corpusOurs >= corpusReference - CorpusMeanMarginDb, meanMessage);

        string verdict = string.Create(
            CultureInfo.InvariantCulture,
            $"- `{format}`: corpus PSNR ours {ParityMetrics.FormatPsnr(corpusOurs)} vs texconv {ParityMetrics.FormatPsnr(corpusReference)} over {measured.Count} images; min per-image margin {FormatMargin(minMargin)} dB (limit -{PerImageMarginDb}) -> PASS.");

        this.summary.AddVerdict(verdict);

        if (!isBc7)
        {
            return;
        }

        double corpusQuickOurs = CorpusPsnr(measured.Select(entry => entry.QuickOurs));
        double corpusQuickReference = CorpusPsnr(measured.Select(entry => entry.QuickReference));
        double minQuickMargin = Minimum(measured.Select(entry => entry.QuickOurs.Psnr - entry.QuickReference.Psnr));

        string quickMeanMessage = string.Create(
            CultureInfo.InvariantCulture,
            $"BC7_UNORM (quick) corpus PSNR {ParityMetrics.FormatPsnr(corpusQuickOurs)} is more than {CorpusMeanMarginDb} dB below texconv's {ParityMetrics.FormatPsnr(corpusQuickReference)}.");

        Assert.True(corpusQuickOurs >= corpusQuickReference - CorpusMeanMarginDb, quickMeanMessage);

        string defaultOverQuickMessage = string.Create(
            CultureInfo.InvariantCulture,
            $"BC7_UNORM: corpus PSNR of the default search {ParityMetrics.FormatPsnr(corpusOurs)} is not at least {DefaultOverQuickMarginDb} dB above the quick search {ParityMetrics.FormatPsnr(corpusQuickOurs)}.");

        Assert.True(corpusOurs >= corpusQuickOurs + DefaultOverQuickMarginDb, defaultOverQuickMessage);

        string quickVerdict = string.Create(
            CultureInfo.InvariantCulture,
            $"- `BC7_UNORM` (quick): corpus PSNR ours {ParityMetrics.FormatPsnr(corpusQuickOurs)} vs texconv {ParityMetrics.FormatPsnr(corpusQuickReference)}; min per-image margin {FormatMargin(minQuickMargin)} dB (limit -{PerImageMarginDb}) -> PASS.");

        this.summary.AddVerdict(quickVerdict);

        string defaultVerdict = string.Create(
            CultureInfo.InvariantCulture,
            $"- `BC7_UNORM` default over quick: {corpusOurs - corpusQuickOurs:+0.000;-0.000} dB (limit +{DefaultOverQuickMarginDb}) -> PASS.");

        this.summary.AddVerdict(defaultVerdict);
    }

    /// <summary>
    ///     The encoder must be deterministic: encoding the same corpus image twice yields the same payload.
    /// </summary>
    [TexconvFact]
    public void EncodingIsDeterministic()
    {
        ParityImage image = ParityCorpus.Get("gradient-256");
        using ParityWorkspace workspace = ParityWorkspace.Create("encode-determinism");

        string first = workspace.PathFor("first.dds");
        string second = workspace.PathFor("second.dds");
        using Image<Rgba32> source = Image.Load<Rgba32>(image.Path);

        EncodeOurs(source, "BC7_UNORM", first, Bc7EncodingQuality.Default);
        EncodeOurs(source, "BC7_UNORM", second, Bc7EncodingQuality.Default);

        Assert.Equal(File.ReadAllBytes(first), File.ReadAllBytes(second));
        this.output.WriteLine("BC7_UNORM encode of gradient-256 is byte-identical across runs.");
        workspace.MarkSuccess();
    }

    private static void EncodeOurs(Image<Rgba32> source, string format, string path, Bc7EncodingQuality quality)
    {
        using Texture texture = new(TextureConfiguration.Default, new TextureMetadata());
        TextureMipLevel mip = texture.AddMipLevel(source.Width, source.Height, 1);
        TextureSurface surface = mip.CreateSurface(TexelFormat.R8G8B8A8_UNorm);
        surface.CopyFrom(source);

        texture.Save(
            path,
            new DdsTextureEncoder(new DdsEncodingOptions
            {
                TargetTexelFormat = ParseFormat(format), Bc7Quality = quality
            }));
    }

    /// <summary>
    ///     Encodes a corpus image with texconv and decodes the result back to RGBA32. Decoding both payloads with the
    ///     same tool keeps the comparison about encoder quality rather than decoder rounding.
    /// </summary>
    private static byte[] EncodeAndDecodeWithTexconv(
        ParityWorkspace workspace,
        ParityImage image,
        string format,
        bool quick,
        string tag)
    {
        Directory.CreateDirectory(workspace.PathFor(tag));

        string[] arguments = quick
            ? ["-ft", "dds", "-f", format, "-bc", "q", "-m", "1", "-o", tag, "--", image.Path]
            : ["-ft", "dds", "-f", format, "-m", "1", "-o", tag, "--", image.Path];

        TexconvProcess.Run(workspace, arguments);

        string ddsPath =
            workspace.PathFor(Path.Combine(tag, Path.ChangeExtension(Path.GetFileName(image.Path), ".dds")));

        return DecodeWithTexconv(workspace, ddsPath, tag + "-decode");
    }

    private static byte[] DecodeWithTexconv(ParityWorkspace workspace, string ddsPath, string tag)
    {
        Directory.CreateDirectory(workspace.PathFor(tag));

        TexconvProcess.Run(workspace, "-ft", "tga", "-f", "R8G8B8A8_UNORM", "-o", tag, "--", ddsPath);

        string tgaPath = workspace.PathFor(Path.Combine(tag, Path.ChangeExtension(Path.GetFileName(ddsPath), ".tga")));

        using Image<Rgba32> decoded = Image.Load<Rgba32>(tgaPath);
        return CopyPixels(decoded);
    }

    private static byte[] DecodeOurs(string ddsPath)
    {
        using Texture texture = Texture.Load(ddsPath);
        TextureSurface surface = texture.MipLevels[0].Surfaces[0];

        using Image<Rgba32> decoded = surface.ToImage<Rgba32>();
        return CopyPixels(decoded);
    }

    private static Quality Measure(byte[] source, byte[] candidate, ParityImage image, ParityChannels channels)
    {
        long squaredError = 0;
        int samples = 0;
        int maxDiff = 0;

        for (int i = 0; i < image.Width * image.Height; i++)
        {
            int offset = i * 4;

            for (int channel = 0; channel < 3; channel++)
            {
                ParityChannels flag = channel switch
                {
                    0 => ParityChannels.Red,
                    1 => ParityChannels.Green,
                    _ => ParityChannels.Blue
                };

                if ((channels & flag) == 0)
                {
                    continue;
                }

                int diff = source[offset + channel] - candidate[offset + channel];
                squaredError += (long)diff * diff;
                samples++;

                if (Math.Abs(diff) > maxDiff)
                {
                    maxDiff = Math.Abs(diff);
                }
            }
        }

        return new Quality(maxDiff, PsnrOf(squaredError, samples), squaredError, samples);
    }

    private static double PsnrOf(long squaredError, int samples)
        => squaredError == 0 ? double.PositiveInfinity : 10 * Math.Log10(255.0 * 255.0 * samples / squaredError);

    private static double CorpusPsnr(IEnumerable<Quality> values)
    {
        long squaredError = 0;
        long samples = 0;

        foreach (Quality value in values)
        {
            squaredError += value.SquaredError;
            samples += value.Samples;
        }

        return samples == 0 ? double.NaN : PsnrOf(squaredError, (int)Math.Min(samples, int.MaxValue));
    }

    private static double Minimum(IEnumerable<double> values)
    {
        double minimum = double.PositiveInfinity;

        foreach (double value in values)
        {
            if (!double.IsNaN(value) && value < minimum)
            {
                minimum = value;
            }
        }

        return minimum;
    }

    private static string FormatMargin(double margin)
        => double.IsPositiveInfinity(margin)
            ? "n/a"
            : margin.ToString("+0.000;-0.000", CultureInfo.InvariantCulture);

    private static void AssertDeclaredChannels(string format, byte[] rgba, ParityImage image)
    {
        for (int i = 0; i < rgba.Length; i += 4)
        {
            if (format == "BC4_UNORM")
            {
                Assert.True(
                    rgba[i + 1] == 0 && rgba[i + 2] == 0,
                    $"BC4 texel {i / 4} of '{image.Name}': green/blue are {rgba[i + 1]}/{rgba[i + 2]}, expected 0.");
            }
            else
            {
                Assert.True(
                    rgba[i + 2] == 0,
                    $"BC5 texel {i / 4} of '{image.Name}': blue is {rgba[i + 2]}, expected 0.");
            }

            Assert.True(
                rgba[i + 3] == 255,
                $"{format} texel {i / 4} of '{image.Name}': alpha is {rgba[i + 3]}, expected 255.");
        }
    }

    private static ParityChannels ChannelsFor(string format) => format switch
    {
        "BC1_UNORM" => ParityChannels.Bc1,
        "BC4_UNORM" => ParityChannels.Bc4,
        "BC5_UNORM" => ParityChannels.Bc5,
        _ => ParityChannels.Bc7
    };

    private static string ChannelName(string format) => format switch
    {
        "BC4_UNORM" => "R",
        "BC5_UNORM" => "RG",
        _ => "RGB"
    };

    private static TexelFormat ParseFormat(string format) => format switch
    {
        "BC1_UNORM" => TexelFormat.Bc1_UNorm,
        "BC4_UNORM" => TexelFormat.Bc4_UNorm,
        "BC5_UNORM" => TexelFormat.Bc5_UNorm,
        _ => TexelFormat.Bc7_UNorm
    };

    private static byte[] CopyPixels(Image<Rgba32> image)
    {
        byte[] bytes = new byte[image.Width * image.Height * 4];
        image.CopyPixelDataTo(bytes);
        return bytes;
    }

    private readonly record struct Quality(int MaxChannelDiff, double Psnr, long SquaredError, int Samples);

    private readonly record struct Measurement(
        ParityImage Image,
        Quality Ours,
        Quality Reference,
        Quality QuickOurs,
        Quality QuickReference);
}
