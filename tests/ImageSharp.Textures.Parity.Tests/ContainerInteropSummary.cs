using System.Globalization;
using System.Text;

namespace ImageSharp.Textures.Parity.Tests;

/// <summary>
///     Collects the container interop findings and writes <c>artifacts/parity/container-summary.md</c>.
/// </summary>
public sealed class ContainerInteropSummary : IDisposable
{
    private readonly List<string> lines = new();

    /// <inheritdoc />
    public void Dispose()
    {
        // A run where every case was skipped (texconv missing) must not replace the recorded findings.
        if (this.lines.Count == 0)
        {
            return;
        }

        string directory = Path.Combine(TexconvLocator.RepoRoot, "artifacts", "parity");
        Directory.CreateDirectory(directory);

        StringBuilder builder = new();
        builder.AppendLine("# texconv DDS container interop");
        builder.AppendLine();
        builder.AppendLine(CultureInfo.InvariantCulture,
            $"Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC by `Category=ContainerInterop`.");
        builder.AppendLine();

        foreach (string line in this.lines)
        {
            builder.AppendLine(line);
        }

        File.WriteAllText(Path.Combine(directory, "container-summary.md"), builder.ToString());
    }

    /// <summary>
    ///     Records one finding.
    /// </summary>
    /// <param name="line">The markdown line.</param>
    internal void Add(string line)
    {
        lock (this.lines)
        {
            this.lines.Add(line);
        }
    }
}
