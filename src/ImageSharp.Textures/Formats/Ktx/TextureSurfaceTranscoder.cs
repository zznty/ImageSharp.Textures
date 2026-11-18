namespace ImageSharp.Textures.Formats;

/// <summary>
///     Converts a texture surface from its stored texel format into the texel format of a container.
/// </summary>
internal static class TextureSurfaceTranscoder
{
    /// <summary>
    ///     Writes one surface payload in the requested texel format.
    /// </summary>
    /// <param name="surface">The source surface; it is not modified.</param>
    /// <param name="targetFormat">The texel format to write.</param>
    /// <param name="destination">The destination buffer, sized for the target format.</param>
    /// <exception cref="InvalidOperationException">The destination length does not match the target format.</exception>
    internal static void Write(TextureSurface surface, TexelFormat targetFormat, Span<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(surface);

        TexelOperations operations = TexelFormatRegistry.GetOperations(targetFormat);
        int expected = operations.GetEncodedByteCount(surface.Width, surface.Height);
        if (destination.Length != expected)
        {
            throw new InvalidOperationException(
                $"The destination buffer is {destination.Length} bytes but a {surface.Width}x{surface.Height} "
                + $"'{targetFormat}' surface requires {expected} bytes.");
        }

        if (surface.TexelFormat == targetFormat)
        {
            surface.GetPayload().CopyTo(destination);
            return;
        }

        // Conversion goes through RGBA so that texel formats with different channel semantics (for example
        // BC5 to BC1) are converted component by component instead of bit by bit.
        using Image<Rgba32> image = surface.ToImage<Rgba32>();
        operations.Encode(image, destination);
    }
}
