using System.Diagnostics;
using ImageSharp.Textures.Parity.Tests.TestUtilities;
using Xunit.Sdk;

namespace ImageSharp.Textures.Parity.Tests;

/// <summary>
///     Runs the texconv executable inside a per-test workspace.
/// </summary>
internal static class TexconvProcess
{
    /// <summary>
    ///     Runs texconv with the given arguments and returns the combined output.
    /// </summary>
    /// <param name="workspace">The workspace used as the working directory.</param>
    /// <param name="arguments">The texconv arguments, without <c>-y</c>.</param>
    /// <returns>The combined standard output and standard error.</returns>
    /// <exception cref="XunitException">texconv exited with a non-zero code.</exception>
    internal static string Run(ParityWorkspace workspace, params string[] arguments)
    {
        if (!TexconvLocator.IsAvailable)
        {
            throw new InvalidOperationException(TexconvLocator.SkipReason);
        }

        List<string> effective = new(arguments.Length + 2) { "-y" };

        // texconv needs the option terminator before a file path that starts with '/'.
        bool terminated = arguments.Contains("--");
        bool inserted = false;

        foreach (string argument in arguments)
        {
            if (!terminated && !inserted && argument.StartsWith('/'))
            {
                effective.Add("--");
                inserted = true;
            }

            effective.Add(argument);
        }

        ProcessStartInfo startInfo = new(TexconvLocator.Path!)
        {
            WorkingDirectory = workspace.DirectoryPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (string argument in effective)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
                                ?? throw new InvalidOperationException(
                                    $"Could not start texconv at '{TexconvLocator.Path}'.");

        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        string output = standardOutput.GetAwaiter().GetResult() + standardError.GetAwaiter().GetResult();

        if (process.ExitCode != 0)
        {
            throw new XunitException(
                $"texconv {string.Join(' ', effective)} failed with exit code {process.ExitCode}.{Environment.NewLine}{output}");
        }

        return output;
    }
}
