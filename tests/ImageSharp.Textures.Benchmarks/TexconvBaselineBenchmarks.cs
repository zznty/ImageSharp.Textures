using System.Diagnostics;
using System.Globalization;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Tga;

namespace ImageSharp.Textures.Benchmarks;

/// <summary>
///     End-to-end timings of the pinned texconv executable, measured the same way as any other benchmark:
///     one BenchmarkDotNet iteration is one process invocation.
/// </summary>
/// <remarks>
///     <para>
///         These numbers include what our in-process <see cref="BcDecodeBenchmarks" /> and
///         <see cref="BcEncodeBenchmarks" /> deliberately exclude: process creation and tool start-up (quantified
///         separately by <see cref="ProcessStartupFloor" />), reading the input file, writing the output file (a 4 MB
///         TGA at 1024x1024 and 67 MB at 4096x4096) and the interchange conversion the CLI is asked to do. They are
///         therefore <b>not</b> a like-for-like codec comparison; the honest bound is
///         <c>texconv cost - startup floor</c>, and even that bound still contains the file I/O.
///     </para>
///     <para>
///         The inputs are generated once per benchmark process: a deterministic RGBA TGA is written with ImageSharp and
///         encoded into one-mip DDS files by texconv itself (BC7 inputs use <c>-bc q</c>, which only affects how the
///         input was produced, not how long it takes to decode). One surface per file, no mip generation, so the work
///         matches the single-surface arms of the in-process benchmarks.
///     </para>
/// </remarks>
[MemoryDiagnoser]
[BenchmarkCategory("Texconv")]
public class TexconvBaselineBenchmarks
{
    private const string DecodeOutputFormat = "R8G8B8A8_UNORM";

    private static readonly string[] EncodeFormats = ["BC1_UNORM", "BC4_UNORM", "BC5_UNORM"];

    private static readonly Lazy<Baseline> Prepared = new(Prepare, LazyThreadSafetyMode.ExecutionAndPublication);

    private Baseline baseline = null!;

    /// <summary>
    ///     Prepares the corpus once per benchmark process and fails loudly when texconv is missing.
    /// </summary>
    [GlobalSetup]
    public void Setup() => this.baseline = Prepared.Value;

    /// <summary>
    ///     Measures process start-up by running texconv's help output, which exits with code 1 by design.
    /// </summary>
    [Benchmark]
    public void ProcessStartupFloor()
    {
        Baseline baseline = this.baseline;
        string output = baseline.RunRaw("-h");

        if (!output.Contains("DirectXTex", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"texconv -h did not print the DirectXTex banner:{Environment.NewLine}{output}");
        }
    }

    /// <summary>
    ///     Decodes a one-mip DDS to an RGBA TGA with texconv.
    /// </summary>
    /// <param name="format">The texconv format spelling of the input DDS.</param>
    [Benchmark]
    [Arguments("BC1_UNORM")]
    [Arguments("BC4_UNORM")]
    [Arguments("BC5_UNORM")]
    [Arguments("BC7_UNORM")]
    public void Decode1024(string format)
    {
        Baseline baseline = this.baseline;
        string directory = baseline.OutputDirectory("decode-" + format);
        string input = baseline.Input1024(format);
        baseline.Run(Path.Combine(directory, "src1024.tga"), "-ft", "tga", "-f", DecodeOutputFormat, "-o", directory,
            "--", input);
    }

    /// <summary>
    ///     Decodes a one-mip 4096x4096 BC7 DDS to an RGBA TGA with texconv.
    /// </summary>
    [Benchmark]
    public void Decode4096()
    {
        Baseline baseline = this.baseline;
        string directory = baseline.OutputDirectory("decode-bc7-4096");
        baseline.Run(Path.Combine(directory, "src4096.tga"), "-ft", "tga", "-f", DecodeOutputFormat, "-o", directory,
            "--", baseline.Input4096());
    }

    /// <summary>
    ///     Encodes a 1024x1024 RGBA TGA to a single-level DDS with texconv.
    /// </summary>
    /// <param name="format">The texconv format spelling of the output DDS.</param>
    [Benchmark]
    [Arguments("BC1_UNORM")]
    [Arguments("BC4_UNORM")]
    [Arguments("BC5_UNORM")]
    public void Encode1024(string format)
    {
        Baseline baseline = this.baseline;
        string directory = baseline.OutputDirectory("encode-" + format);
        baseline.Run(Path.Combine(directory, "src1024.dds"), "-ft", "dds", "-f", format, "-m", "1", "-o", directory,
            "--", baseline.Source1024);
    }

    /// <summary>
    ///     Encodes a 1024x1024 RGBA TGA to BC7 with the mode 6 preset (<c>-bc q</c>).
    /// </summary>
    [Benchmark]
    public void Encode1024Quick()
    {
        Baseline baseline = this.baseline;
        string directory = baseline.OutputDirectory("encode-bc7-quick");
        baseline.Run(Path.Combine(directory, "src1024.dds"), "-ft", "dds", "-f", "BC7_UNORM", "-m", "1", "-bc", "q",
            "-o", directory, "--", baseline.Source1024);
    }

    /// <summary>
    ///     Encodes a 1024x1024 RGBA TGA to BC7 with the default multi-mode search.
    /// </summary>
    [Benchmark]
    public void Encode1024Default()
    {
        Baseline baseline = this.baseline;
        string directory = baseline.OutputDirectory("encode-bc7-default");
        baseline.Run(Path.Combine(directory, "src1024.dds"), "-ft", "dds", "-f", "BC7_UNORM", "-m", "1", "-o",
            directory, "--", baseline.Source1024);
    }

    private static Baseline Prepare()
    {
        string root = FindRepositoryRoot();
        string texconv = LocateTexconv(root);
        string workspace = Path.Combine(root, "artifacts", "bench", "texconv-baseline");

        if (Directory.Exists(workspace))
        {
            Directory.Delete(workspace, true);
        }

        Directory.CreateDirectory(Path.Combine(workspace, "in"));

        Baseline baseline = new(texconv, workspace);
        baseline.WriteSource(1024);
        baseline.WriteSource(4096);

        foreach (string format in new[] { "BC1_UNORM", "BC4_UNORM", "BC5_UNORM", "BC7_UNORM" })
        {
            string directory = Path.Combine(workspace, "in", format);
            Directory.CreateDirectory(directory);
            string[] arguments = format == "BC7_UNORM"
                ? ["-ft", "dds", "-f", format, "-m", "1", "-bc", "q", "-o", directory, "--", baseline.Source1024]
                : ["-ft", "dds", "-f", format, "-m", "1", "-o", directory, "--", baseline.Source1024];
            baseline.Run(Path.Combine(directory, "src1024.dds"), arguments);
        }

        string large = Path.Combine(workspace, "in", "BC7_UNORM-4096");
        Directory.CreateDirectory(large);
        baseline.Run(
            Path.Combine(large, "src4096.dds"),
            "-ft",
            "dds",
            "-f",
            "BC7_UNORM",
            "-m",
            "1",
            "-bc",
            "q",
            "-o",
            large,
            "--",
            baseline.Source4096);

        return baseline;
    }

    private static string LocateTexconv(string root)
    {
        string? fromEnvironment = Environment.GetEnvironmentVariable("TEXCONV_PATH");
        if (!string.IsNullOrWhiteSpace(fromEnvironment) && File.Exists(fromEnvironment))
        {
            return fromEnvironment;
        }

        string bundled = Path.Combine(root, "artifacts", "texconv", "texconv");
        if (File.Exists(bundled))
        {
            return bundled;
        }

        throw new InvalidOperationException(
            $"texconv not found. Set TEXCONV_PATH or run scripts/build-texconv.sh; looked at '{fromEnvironment}' and '{bundled}'.");
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ImageSharp.Textures.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate the repository root (no ImageSharp.Textures.sln) above '{AppContext.BaseDirectory}'.");
    }

    /// <summary>
    ///     The prepared inputs and the runner shared by every benchmark case in the process.
    /// </summary>
    private sealed class Baseline
    {
        private readonly string texconv;
        private readonly string workspace;

        internal Baseline(string texconv, string workspace)
        {
            this.texconv = texconv;
            this.workspace = workspace;
            this.Source1024 = Path.Combine(workspace, "in", "src1024.tga");
            this.Source4096 = Path.Combine(workspace, "in", "src4096.tga");
        }

        internal string Source1024 { get; }

        internal string Source4096 { get; }

        internal string Input1024(string format) => Path.Combine(this.workspace, "in", format, "src1024.dds");

        internal string Input4096() => Path.Combine(this.workspace, "in", "BC7_UNORM-4096", "src4096.dds");

        internal string OutputDirectory(string name)
        {
            string directory = Path.Combine(this.workspace, "out", name);
            Directory.CreateDirectory(directory);
            return directory;
        }

        internal void WriteSource(int size)
        {
            using Image<Rgba32> image = new(size, size);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    image[x, y] = new Rgba32(
                        (byte)(x * 255 / Math.Max(1, size - 1)),
                        (byte)(y * 255 / Math.Max(1, size - 1)),
                        (byte)((x + y) * 255 / Math.Max(1, (2 * size) - 2)),
                        255);
                }
            }

            string path = Path.Combine(this.workspace, "in",
                string.Create(CultureInfo.InvariantCulture, $"src{size}.tga"));
            image.Save(path,
                new TgaEncoder { BitsPerPixel = TgaBitsPerPixel.Bit32, Compression = TgaCompression.None });
        }

        /// <summary>
        ///     Runs texconv once and asserts the expected output file was produced and is not empty.
        /// </summary>
        /// <param name="expectedOutput">The absolute path of the file the invocation must produce.</param>
        /// <param name="arguments">The texconv arguments, without <c>-y</c>.</param>
        internal void Run(string expectedOutput, params string[] arguments)
        {
            string output = this.RunRaw(arguments);

            FileInfo file = new(expectedOutput);
            if (!file.Exists || file.Length == 0)
            {
                throw new InvalidOperationException(
                    $"texconv did not produce '{expectedOutput}'.{Environment.NewLine}{output}");
            }
        }

        /// <summary>
        ///     Runs texconv once and throws with the combined output when it exits non-zero.
        /// </summary>
        /// <param name="arguments">The texconv arguments.</param>
        /// <returns>The combined standard output and standard error.</returns>
        internal string RunRaw(params string[] arguments)
        {
            ProcessStartInfo startInfo = new(this.texconv)
            {
                WorkingDirectory = this.workspace,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            startInfo.ArgumentList.Add("-y");
            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using Process process = Process.Start(startInfo)
                                    ?? throw new InvalidOperationException(
                                        $"Could not start texconv at '{this.texconv}'.");

            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
            Task<string> standardError = process.StandardError.ReadToEndAsync();
            process.WaitForExit();

            string combined = standardOutput.GetAwaiter().GetResult() + standardError.GetAwaiter().GetResult();

            if (process.ExitCode != 0 && !arguments.SequenceEqual(["-h"]))
            {
                throw new InvalidOperationException(
                    $"texconv {string.Join(' ', arguments)} failed with exit code {process.ExitCode}.{Environment.NewLine}{combined}");
            }

            return combined;
        }
    }
}
