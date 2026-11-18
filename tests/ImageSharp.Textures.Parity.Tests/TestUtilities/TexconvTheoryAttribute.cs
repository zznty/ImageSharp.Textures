using Xunit.Sdk;

namespace ImageSharp.Textures.Parity.Tests.TestUtilities;

/// <summary>
///     A theory that is skipped at discovery time when texconv cannot be resolved.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
[XunitTestCaseDiscoverer(
    "ImageSharp.Textures.Parity.Tests.TestUtilities.TexconvTheoryDiscoverer",
    "ImageSharp.Textures.Parity.Tests")]
public sealed class TexconvTheoryAttribute : TheoryAttribute
{
}
