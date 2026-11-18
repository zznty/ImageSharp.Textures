using System.Globalization;
using System.Text;

namespace ImageSharp.Textures.Parity.Tests;

/// <summary>
///     Collects the decode parity rows and writes <c>artifacts/parity/decode-summary.md</c> after the class runs.
/// </summary>
public sealed class DecodeParitySummary : IDisposable
{
    private readonly List<(string Format, ParityImage Image, ParityDiff Diff)> rows = new();

    /// <inheritdoc />
    public void Dispose()
    {
        // A run where every case was skipped (texconv missing) must not replace the recorded table.
        if (this.rows.Count == 0)
        {
            return;
        }

        string directory = Path.Combine(TexconvLocator.RepoRoot, "artifacts", "parity");
        Directory.CreateDirectory(directory);

        StringBuilder builder = new();
        builder.AppendLine("# texconv decode parity");
        builder.AppendLine();
        builder.AppendLine(CultureInfo.InvariantCulture,
            $"Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC by `Category=ParityDecode`.");
        builder.AppendLine();
        builder.AppendLine("| format | image | size | max diff | differing px | RGB PSNR | alpha PSNR |");
        builder.AppendLine("| --- | --- | --- | ---: | ---: | ---: | ---: |");

        foreach ((string format, ParityImage image, ParityDiff diff) in this.rows.OrderBy(row => row.Format)
                     .ThenBy(row => row.Image.Name))
        {
            string head = string.Create(
                CultureInfo.InvariantCulture,
                $"| {format} | {image.Name} | {image.Width}x{image.Height} | {diff.MaxChannelDiff} | {diff.DifferingPixels} | {ParityMetrics.FormatPsnr(diff.RgbPsnr)}");
            string tail = string.Create(CultureInfo.InvariantCulture,
                $" | {ParityMetrics.FormatPsnr(diff.AlphaPsnr)} |");
            string row = head + tail;

            builder.AppendLine(row);
        }

        File.WriteAllText(Path.Combine(directory, "decode-summary.md"), builder.ToString());
    }

    /// <summary>
    ///     Records one completed comparison.
    /// </summary>
    /// <param name="format">The texconv format spelling.</param>
    /// <param name="image">The corpus image.</param>
    /// <param name="diff">The measured difference.</param>
    internal void Add(string format, ParityImage image, ParityDiff diff)
    {
        lock (this.rows)
        {
            this.rows.Add((format, image, diff));
        }
    }
}
