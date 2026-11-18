namespace ImageSharp.Textures.Tests.TestUtilities.Ktx2;

/// <summary>
///     One level index entry produced by <see cref="Ktx2FileBuilder" />.
/// </summary>
/// <param name="ByteOffset">The absolute level offset.</param>
/// <param name="ByteLength">The stored level byte length.</param>
/// <param name="UncompressedByteLength">The uncompressed level byte length.</param>
internal readonly record struct Ktx2LevelIndexEntry(long ByteOffset, long ByteLength, long UncompressedByteLength);
