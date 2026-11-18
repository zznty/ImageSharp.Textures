namespace ImageSharp.Textures.Tests.TestUtilities;

/// <summary>
///     A minimal texture format used to exercise the format plumbing without a container codec.
/// </summary>
internal sealed class TestTextureFormat : ITextureFormat<TestTextureFormatMetadata>
{
    private readonly string[] extensions;

    public TestTextureFormat(string name, string defaultMimeType, params string[] extensions)
    {
        this.Name = name;
        this.DefaultMimeType = defaultMimeType;
        this.extensions = extensions;
    }

    public string Name { get; }

    public string DefaultMimeType { get; }

    public IEnumerable<string> MimeTypes => [this.DefaultMimeType];

    public IEnumerable<string> FileExtensions => this.extensions;

    public TestTextureFormatMetadata CreateDefaultFormatMetadata() => new();
}
