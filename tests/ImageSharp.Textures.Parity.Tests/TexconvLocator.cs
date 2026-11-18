namespace ImageSharp.Textures.Parity.Tests;

/// <summary>
///     Resolves the texconv executable and the repository root used by the parity suite.
/// </summary>
/// <remarks>
///     The search order is <c>TEXCONV_PATH</c>, then <c>artifacts/texconv/texconv</c> (the output of
///     <c>scripts/build-texconv.sh</c>). The result is resolved once per test run.
/// </remarks>
internal static class TexconvLocator
{
    /// <summary>
    ///     The reason reported by the skip attributes when no executable is found.
    /// </summary>
    internal const string SkipReason = "texconv not available; run scripts/build-texconv.sh";

    private static readonly Lazy<Resolution> Resolved = new(Locate, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    ///     Gets the repository root, discovered by walking up from the test assembly location.
    /// </summary>
    internal static string RepoRoot => Resolved.Value.RepoRoot
                                       ?? throw new InvalidOperationException(
                                           $"Could not locate the repository root (no ImageSharp.Textures.slnx) above '{AppContext.BaseDirectory}'.");

    /// <summary>
    ///     Gets the resolved texconv executable, or <see langword="null" /> when none is available.
    /// </summary>
    internal static string? Path => Resolved.Value.Executable;

    /// <summary>
    ///     Gets a value indicating whether a texconv executable was resolved.
    /// </summary>
    internal static bool IsAvailable => Resolved.Value.Executable is not null;

    private static Resolution Locate()
    {
        string? root = FindRepositoryRoot();
        if (root is null)
        {
            return new Resolution(null, null);
        }

        string? fromEnvironment = Environment.GetEnvironmentVariable("TEXCONV_PATH");
        if (!string.IsNullOrWhiteSpace(fromEnvironment) && IsExecutable(fromEnvironment))
        {
            return new Resolution(root, fromEnvironment);
        }

        string bundled = System.IO.Path.Combine(root, "artifacts", "texconv", "texconv");
        if (IsExecutable(bundled))
        {
            return new Resolution(root, bundled);
        }

        return new Resolution(root, null);
    }

    private static bool IsExecutable(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        if (OperatingSystem.IsWindows())
        {
            return true;
        }

        UnixFileMode mode = File.GetUnixFileMode(path);
        return (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
    }

    private static string? FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(System.IO.Path.Combine(directory.FullName, "ImageSharp.Textures.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private readonly record struct Resolution(string? RepoRoot, string? Executable);
}
