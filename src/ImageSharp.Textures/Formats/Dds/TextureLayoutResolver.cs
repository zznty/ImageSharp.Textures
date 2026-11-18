namespace ImageSharp.Textures.Formats.Dds;

/// <summary>
///     Resolves the face layout a texture carries in its container metadata.
/// </summary>
/// <remarks>
///     A texture loaded from one container keeps that container's metadata when it is saved to another, so an
///     encoder cannot assume its own metadata is present. Every metadata object that implements
///     <see cref="ITextureFaceLayoutMetadata" /> contributes a claim; all claims must agree, and a disagreement is
///     reported instead of being resolved silently.
/// </remarks>
internal static class TextureLayoutResolver
{
    /// <summary>
    ///     Resolves the face layout declared by the texture metadata.
    /// </summary>
    /// <param name="texture">The texture whose metadata is read.</param>
    /// <returns>The declared layout, or <see cref="TextureLayout.Unclaimed" /> when no metadata claims one.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Two metadata objects disagree about the face count, or a claim is out of range.
    /// </exception>
    internal static TextureLayout Resolve(Texture texture)
    {
        ArgumentNullException.ThrowIfNull(texture);

        TextureLayout resolved = TextureLayout.Unclaimed;

        foreach (ITextureFormatMetadata metadata in texture.Metadata.FormatMetadataEntries)
        {
            if (metadata is not ITextureFaceLayoutMetadata layout)
            {
                continue;
            }

            string source = metadata.GetType().Name;

            if (layout.FaceCount is not (1 or 6))
            {
                throw new InvalidOperationException(
                    $"The '{source}' texture metadata declares {layout.FaceCount} cube map faces; only 1 or 6 are valid.");
            }

            if (resolved.HasClaim && resolved.FaceCount != layout.FaceCount)
            {
                throw new InvalidOperationException(
                    $"The texture metadata disagrees about the cube map layout: '{resolved.Source}' declares "
                    + $"{resolved.FaceCount} face(s) while '{source}' declares {layout.FaceCount}. "
                    + "Remove the stale metadata or clear the conflicting container.");
            }

            resolved = new TextureLayout(layout.FaceCount, source);
        }

        return resolved;
    }
}
