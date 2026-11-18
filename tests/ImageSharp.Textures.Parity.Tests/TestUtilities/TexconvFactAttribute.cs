using Xunit.Sdk;

namespace ImageSharp.Textures.Parity.Tests.TestUtilities;

/// <summary>
///     A fact that is skipped at discovery time when texconv cannot be resolved.
/// </summary>
/// <remarks>
///     xunit v2 reads <see cref="FactAttribute.Skip" /> from the attribute metadata, not from the attribute
///     instance, so the skip reason is applied by <see cref="TexconvFactDiscoverer" /> instead.
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
[XunitTestCaseDiscoverer(
    "ImageSharp.Textures.Parity.Tests.TestUtilities.TexconvFactDiscoverer",
    "ImageSharp.Textures.Parity.Tests")]
public sealed class TexconvFactAttribute : FactAttribute
{
}
