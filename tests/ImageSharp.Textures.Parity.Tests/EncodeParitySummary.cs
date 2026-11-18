using System.Globalization;
using System.Text;

namespace ImageSharp.Textures.Parity.Tests;

/// <summary>
///     Collects the encode parity rows and writes <c>artifacts/parity/encode-summary.md</c> after the class runs.
/// </summary>
/// <remarks>
///     A row holds the PSNR (over each format's declared colour channels) of our encode and of texconv's encode,
///     both decoded by texconv, measured against the corpus image. BC7 rows additionally carry the quick (mode 6)
///     numbers. Gate verdicts are recorded as they are evaluated.
/// </remarks>
public sealed class EncodeParitySummary : IDisposable
{
    private readonly List<Row> rows = new();
    private readonly List<string> verdicts = new();

    /// <inheritdoc />
    public void Dispose()
    {
        string directory = Path.Combine(TexconvLocator.RepoRoot, "artifacts", "parity");
        Directory.CreateDirectory(directory);

        StringBuilder builder = new();
        builder.AppendLine("# texconv encode quality parity");
        builder.AppendLine();
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC by `Category=EncodeParity`."));
        builder.AppendLine();
        builder.AppendLine(
            "PSNR is measured between the texconv decode of each encode and the corpus source, over the format's declared colour channels (BC1/BC7: RGB, BC4: R, BC5: R and G). `quick` is the BC7 mode 6 preset (`-bc q`).");
        builder.AppendLine();
        builder.AppendLine(
            "| format | image | size | channels | PSNR ours | PSNR texconv | quick ours | quick texconv | max source diff |");
        builder.AppendLine("| --- | --- | --- | --- | ---: | ---: | ---: | ---: | ---: |");

        foreach (Row row in this.rows.OrderBy(entry => entry.Format)
                     .ThenBy(entry => entry.Image.Name, StringComparer.Ordinal))
        {
            string line = string.Create(
                CultureInfo.InvariantCulture,
                $"| {row.Format} | {row.Image.Name} | {row.Image.Width}x{row.Image.Height} | {row.Channels} | {ParityMetrics.FormatPsnr(row.Ours)} | {ParityMetrics.FormatPsnr(row.Texconv)} | {ParityMetrics.FormatPsnr(row.QuickOurs)} | {ParityMetrics.FormatPsnr(row.QuickTexconv)} | {row.SourceDiff} |");

            builder.AppendLine(line);
        }

        builder.AppendLine();
        builder.AppendLine("## Gates");
        builder.AppendLine();

        foreach (string verdict in this.verdicts)
        {
            builder.AppendLine(verdict);
        }

        File.WriteAllText(Path.Combine(directory, "encode-summary.md"), builder.ToString());
    }

    /// <summary>
    ///     Records one completed comparison.
    /// </summary>
    /// <param name="format">The texconv format spelling.</param>
    /// <param name="image">The corpus image.</param>
    /// <param name="channels">The channels the PSNR was measured over, as a display name.</param>
    /// <param name="ours">The PSNR of our encode.</param>
    /// <param name="texconv">The PSNR of texconv's encode.</param>
    /// <param name="quickOurs">The PSNR of our quick encode, or NaN when the format has no quick preset.</param>
    /// <param name="quickTexconv">The PSNR of texconv's quick encode, or NaN when the format has no quick preset.</param>
    /// <param name="sourceDiff">The largest per-channel difference between the decoded image and the source.</param>
    internal void Add(
        string format,
        ParityImage image,
        string channels,
        double ours,
        double texconv,
        double quickOurs,
        double quickTexconv,
        int sourceDiff)
    {
        lock (this.rows)
        {
            this.rows.Add(new Row(format, image, channels, ours, texconv, quickOurs, quickTexconv, sourceDiff));
        }
    }

    /// <summary>
    ///     Records one gate verdict for the summary.
    /// </summary>
    /// <param name="line">The markdown line.</param>
    internal void AddVerdict(string line)
    {
        lock (this.verdicts)
        {
            this.verdicts.Add(line);
        }
    }

    private readonly record struct Row(
        string Format,
        ParityImage Image,
        string Channels,
        double Ours,
        double Texconv,
        double QuickOurs,
        double QuickTexconv,
        int SourceDiff);
}
