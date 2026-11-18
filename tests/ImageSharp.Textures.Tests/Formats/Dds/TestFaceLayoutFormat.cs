namespace ImageSharp.Textures.Tests.Formats.Dds;

/// <summary>
///     A stand-in container format used to attach face layout metadata to a texture that was built by the test
///     factory instead of being decoded from a container.
/// </summary>
internal sealed class TestFaceLayoutFormat : ITextureFormat<TestFaceLayoutMetadata>
{
    private static readonly string[] MimeTypeValues = ["image/x-face-layout"];
    private static readonly string[] FileExtensionValues = [".facelayout"];

    private TestFaceLayoutFormat()
    {
    }

    /// <summary>
    ///     Gets the shared instance.
    /// </summary>
    internal static TestFaceLayoutFormat Instance { get; } = new();

    /// <inheritdoc />
    public string Name => "FACELAYOUT";

    /// <inheritdoc />
    public string DefaultMimeType => "image/x-face-layout";

    /// <inheritdoc />
    public IEnumerable<string> MimeTypes => MimeTypeValues;

    /// <inheritdoc />
    public IEnumerable<string> FileExtensions => FileExtensionValues;

    /// <inheritdoc />
    public TestFaceLayoutMetadata CreateDefaultFormatMetadata() => new();
}
