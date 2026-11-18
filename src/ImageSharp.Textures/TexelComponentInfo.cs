namespace ImageSharp.Textures;

/// <summary>
///     Describes the components that make up a texel type.
/// </summary>
public readonly struct TexelComponentInfo : IEquatable<TexelComponentInfo>
{
    private readonly int[]? precisions;
    private readonly TexelComponentInterpretation[]? interpretations;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TexelComponentInfo" /> struct.
    /// </summary>
    /// <param name="componentPrecisions">The precision, in bits, of each component.</param>
    /// <param name="componentInterpretations">The numeric interpretation of each component.</param>
    /// <param name="padding">The number of padding bits in the texel type.</param>
    public TexelComponentInfo(
        ReadOnlySpan<int> componentPrecisions,
        ReadOnlySpan<TexelComponentInterpretation> componentInterpretations,
        int padding = 0)
    {
        if (componentPrecisions.Length != componentInterpretations.Length)
        {
            throw new ArgumentException(
                "The component precision and interpretation counts must match.",
                nameof(componentInterpretations));
        }

        if (componentPrecisions.IsEmpty)
        {
            throw new ArgumentException("A texel type must declare at least one component.",
                nameof(componentPrecisions));
        }

        foreach (int precision in componentPrecisions)
        {
            if (precision <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(componentPrecisions),
                    precision,
                    "Component precisions must be positive.");
            }
        }

        if (padding < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(padding), padding, "Padding must not be negative.");
        }

        this.precisions = componentPrecisions.ToArray();
        this.interpretations = componentInterpretations.ToArray();
        this.Padding = padding;
    }

    /// <summary>
    ///     Gets the number of components in the texel type.
    /// </summary>
    public int ComponentCount => this.precisions?.Length ?? 0;

    /// <summary>
    ///     Gets the number of padding bits in the texel type.
    /// </summary>
    public int Padding { get; }

    /// <summary>
    ///     Determines whether two <see cref="TexelComponentInfo" /> instances are equal.
    /// </summary>
    /// <param name="left">The left instance.</param>
    /// <param name="right">The right instance.</param>
    /// <returns><see langword="true" /> when the instances are equal; otherwise, <see langword="false" />.</returns>
    public static bool operator ==(TexelComponentInfo left, TexelComponentInfo right) => left.Equals(right);

    /// <summary>
    ///     Determines whether two <see cref="TexelComponentInfo" /> instances are not equal.
    /// </summary>
    /// <param name="left">The left instance.</param>
    /// <param name="right">The right instance.</param>
    /// <returns><see langword="true" /> when the instances are not equal; otherwise, <see langword="false" />.</returns>
    public static bool operator !=(TexelComponentInfo left, TexelComponentInfo right) => !left.Equals(right);

    /// <summary>
    ///     Determines whether the specified <see cref="TexelComponentInfo" /> is equal to the current instance.
    /// </summary>
    /// <param name="other">The instance to compare with.</param>
    /// <returns><see langword="true" /> when the instances describe the same components; otherwise, <see langword="false" />.</returns>
    public bool Equals(TexelComponentInfo other)
    {
        if (this.Padding != other.Padding || this.ComponentCount != other.ComponentCount)
        {
            return false;
        }

        for (int i = 0; i < this.ComponentCount; i++)
        {
            if (this.GetComponentPrecision(i) != other.GetComponentPrecision(i)
                || this.GetComponentInterpretation(i) != other.GetComponentInterpretation(i))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is TexelComponentInfo other && this.Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        HashCode hashCode = default;
        hashCode.Add(this.Padding);
        hashCode.Add(this.ComponentCount);

        for (int i = 0; i < this.ComponentCount; i++)
        {
            hashCode.Add(this.GetComponentPrecision(i));
            hashCode.Add(this.GetComponentInterpretation(i));
        }

        return hashCode.ToHashCode();
    }

    /// <summary>
    ///     Gets the component precision at the specified index.
    /// </summary>
    /// <param name="componentIndex">The component index.</param>
    /// <returns>The component precision in bits.</returns>
    public int GetComponentPrecision(int componentIndex)
    {
        this.ValidateComponentIndex(componentIndex);
        return this.precisions![componentIndex];
    }

    /// <summary>
    ///     Gets the numeric interpretation used by the stored component value at the specified index.
    /// </summary>
    /// <param name="componentIndex">The component index.</param>
    /// <returns>The component interpretation.</returns>
    public TexelComponentInterpretation GetComponentInterpretation(int componentIndex)
    {
        this.ValidateComponentIndex(componentIndex);
        return this.interpretations![componentIndex];
    }

    private void ValidateComponentIndex(int componentIndex)
    {
        if ((uint)componentIndex >= (uint)this.ComponentCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(componentIndex),
                componentIndex,
                "The component index must be within the declared component count.");
        }
    }
}
