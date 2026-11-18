namespace ImageSharp.Textures.Parity.Tests;

/// <summary>
///     One deterministic corpus image, generated in code and cached as a TGA.
/// </summary>
/// <param name="Name">The corpus name.</param>
/// <param name="Width">The image width, in pixels.</param>
/// <param name="Height">The image height, in pixels.</param>
/// <param name="Path">The absolute path of the cached TGA.</param>
/// <param name="Hash">The SHA-256 of the encoded TGA bytes.</param>
internal sealed record ParityImage(string Name, int Width, int Height, string Path, string Hash);
