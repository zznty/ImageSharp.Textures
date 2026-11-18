namespace ImageSharp.Textures.Parity.Tests;

/// <summary>
///     The result of comparing two decoded surfaces.
/// </summary>
/// <param name="MaxChannelDiff">The largest per-channel absolute difference over the compared channels.</param>
/// <param name="DifferingPixels">The number of pixels with at least one differing compared channel.</param>
/// <param name="RgbPsnr">The PSNR over the compared colour channels, in dB.</param>
/// <param name="AlphaPsnr">The PSNR over alpha, in dB, or <see cref="double.NaN" /> when alpha is not compared.</param>
internal readonly record struct ParityDiff(int MaxChannelDiff, int DifferingPixels, double RgbPsnr, double AlphaPsnr);
