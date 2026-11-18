using BenchmarkDotNet.Attributes;
using ImageSharp.Textures.Texels.Bc1;
using ImageSharp.Textures.Texels.Bc4;
using ImageSharp.Textures.Texels.Bc5;
using ImageSharp.Textures.Texels.BcCommon;

namespace ImageSharp.Textures.Benchmarks;

/// <summary>
///     Paired BC1/BC4/BC5 encode comparison: our block encoder and the pinned DirectXTex codec measured as two
///     methods of the same class, in one process, on the same packed RGBA32 surface.
/// </summary>
/// <remarks>
///     <para>
///         The separate-class comparison in <see cref="DirectXTexEncodeBaselineBenchmarks" /> and
///         <see cref="BcEncodeBenchmarks" /> crosses sessions, and the shim's own encode moves by ~5 % between sessions,
///         which is the same size as the differences being judged. Here BenchmarkDotNet measures both arms back to back
///         and reports its <c>Ratio</c> column (ours / baseline) with a confidence interval, so the number does not
///         depend on the session.
///     </para>
///     <para>
///         <see cref="GlobalSetup" /> encodes the surface with both codecs once and fails the run when the payloads are
///         not byte-identical, so a paired ratio can never describe two different codecs.
///     </para>
/// </remarks>
[MemoryDiagnoser]
[BenchmarkCategory("DirectXTex", "Paired")]
public class PairedBcEncodeBenchmarks
{
    private const int SurfaceSize = 1024;
    private byte[] ours = null!;

    private byte[] source = null!;
    private byte[] theirs = null!;

    /// <summary>
    ///     Gets or sets the texel format under test.
    /// </summary>
    [Params(TexelFormat.Bc1_UNorm, TexelFormat.Bc4_UNorm, TexelFormat.Bc5_UNorm)]
    public TexelFormat Format { get; set; }

    /// <summary>
    ///     Packs the shared source once, allocates both destinations and verifies that the payloads match.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        DirectXTexShim.EnsureVerified();

        this.source = DirectXTexShim.PackSource(SurfaceSize);
        int blockBytes = BenchmarkData.BlockBytes(this.Format);
        int blocks = SurfaceSize / 4 * (SurfaceSize / 4);
        this.ours = new byte[blocks * blockBytes];
        this.theirs = new byte[blocks * blockBytes];

        this.EncodeOurs();
        this.EncodeDirectXTex();

        int differing = 0;
        for (int i = 0; i < this.ours.Length; i++)
        {
            if (this.ours[i] != this.theirs[i])
            {
                differing++;
            }
        }

        if (differing != 0)
        {
            throw new InvalidOperationException(
                $"Paired encode cannot compare two different codecs: {this.Format} payloads differ in {differing} of "
                + $"{this.ours.Length} bytes on a {SurfaceSize}x{SurfaceSize} surface.");
        }
    }

    /// <summary>
    ///     Encodes with the pinned DirectXTex codec through the shim (the baseline arm).
    /// </summary>
    [Benchmark(Baseline = true)]
    public void DirectXTex()
        => DirectXTexShim.Encode(this.Format, this.source, this.theirs, SurfaceSize, Bc7EncodingQuality.Default);

    /// <summary>
    ///     Encodes with our block encoder from the same packed source.
    /// </summary>
    [Benchmark]
    public void Ours() => this.EncodeOurs();

    private void EncodeOurs()
    {
        switch (this.Format)
        {
            case TexelFormat.Bc1_UNorm:
                Bc1Encoder.Encode(this.source, this.ours, SurfaceSize, SurfaceSize, BcEncodeOptions.Default);
                break;
            case TexelFormat.Bc4_UNorm:
                Bc4Encoder.Encode(this.source, this.ours, SurfaceSize, SurfaceSize, BcEncodeOptions.Default);
                break;
            default:
                Bc5Encoder.Encode(this.source, this.ours, SurfaceSize, SurfaceSize, BcEncodeOptions.Default);
                break;
        }
    }

    private void EncodeDirectXTex()
        => DirectXTexShim.Encode(this.Format, this.source, this.theirs, SurfaceSize, Bc7EncodingQuality.Default);
}
