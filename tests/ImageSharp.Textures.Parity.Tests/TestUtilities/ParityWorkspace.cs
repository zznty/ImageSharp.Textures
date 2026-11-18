namespace ImageSharp.Textures.Parity.Tests.TestUtilities;

/// <summary>
///     A per-test working directory under <c>artifacts/parity/&lt;test&gt;</c>.
/// </summary>
/// <remarks>
///     The directory is removed when the test marks itself successful and is kept, with every intermediate file,
///     when it does not. Diff images and texconv inputs therefore survive a failure.
/// </remarks>
internal sealed class ParityWorkspace : IDisposable
{
    private bool succeeded;

    private ParityWorkspace(string directoryPath) => this.DirectoryPath = directoryPath;

    /// <summary>
    ///     Gets the absolute path of the working directory.
    /// </summary>
    internal string DirectoryPath { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!this.succeeded || !Directory.Exists(this.DirectoryPath))
        {
            return;
        }

        try
        {
            Directory.Delete(this.DirectoryPath, true);
        }
        catch (IOException)
        {
            // A leftover directory is harmless; the next run recreates it.
        }
    }

    /// <summary>
    ///     Creates a clean working directory for the named test.
    /// </summary>
    /// <param name="testName">The test name, used as the directory name.</param>
    /// <returns>The workspace.</returns>
    internal static ParityWorkspace Create(string testName)
    {
        string root = Path.Combine(TexconvLocator.RepoRoot, "artifacts", "parity", Sanitize(testName));

        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }

        Directory.CreateDirectory(root);
        return new ParityWorkspace(root);
    }

    /// <summary>
    ///     Gets the absolute path of a file inside the workspace.
    /// </summary>
    /// <param name="relativePath">The path relative to the workspace.</param>
    /// <returns>The absolute path.</returns>
    internal string PathFor(string relativePath) => Path.Combine(this.DirectoryPath, relativePath);

    /// <summary>
    ///     Marks the test as successful so that the workspace is removed on dispose.
    /// </summary>
    internal void MarkSuccess() => this.succeeded = true;

    private static string Sanitize(string name)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalid.Contains(c) ? '-' : c).ToArray());
    }
}
