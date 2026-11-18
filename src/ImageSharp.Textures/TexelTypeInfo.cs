namespace ImageSharp.Textures;

/// <summary>
///     Describes the stored texel components of a texture surface.
/// </summary>
public readonly struct TexelTypeInfo : IEquatable<TexelTypeInfo>
{
    /// <summary>
    ///     Gets the color depth, in bits per texel.
    /// </summary>
    public int BitsPerTexel { get; init; }

    /// <summary>
    ///     Gets the color type represented by the texel type.
    /// </summary>
    public TexelColorType ColorType { get; init; }

    /// <summary>
    ///     Gets the component information for the texel type.
    /// </summary>
    public TexelComponentInfo ComponentInfo { get; init; }

    /// <summary>
    ///     Gets the alpha transparency behavior.
    /// </summary>
    public TexelAlphaRepresentation AlphaRepresentation { get; init; }

    /// <summary>
    ///     Determines whether two <see cref="TexelTypeInfo" /> instances are equal.
    /// </summary>
    /// <param name="left">The left instance.</param>
    /// <param name="right">The right instance.</param>
    /// <returns><see langword="true" /> when the instances are equal; otherwise, <see langword="false" />.</returns>
    public static bool operator ==(TexelTypeInfo left, TexelTypeInfo right) => left.Equals(right);

    /// <summary>
    ///     Determines whether two <see cref="TexelTypeInfo" /> instances are not equal.
    /// </summary>
    /// <param name="left">The left instance.</param>
    /// <param name="right">The right instance.</param>
    /// <returns><see langword="true" /> when the instances are not equal; otherwise, <see langword="false" />.</returns>
    public static bool operator !=(TexelTypeInfo left, TexelTypeInfo right) => !left.Equals(right);

    /// <summary>
    ///     Determines whether the specified <see cref="TexelTypeInfo" /> is equal to the current instance.
    /// </summary>
    /// <param name="other">The instance to compare with.</param>
    /// <returns><see langword="true" /> when the instances describe the same texel type; otherwise, <see langword="false" />.</returns>
    public bool Equals(TexelTypeInfo other)
        => this.BitsPerTexel == other.BitsPerTexel
           && this.ColorType == other.ColorType
           && this.AlphaRepresentation == other.AlphaRepresentation
           && this.ComponentInfo.Equals(other.ComponentInfo);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is TexelTypeInfo other && this.Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(
        this.BitsPerTexel,
        this.ColorType,
        this.AlphaRepresentation,
        this.ComponentInfo);
}
