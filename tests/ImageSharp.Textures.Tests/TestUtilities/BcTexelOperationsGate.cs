namespace ImageSharp.Textures.Tests.TestUtilities;

/// <summary>
///     Detects whether the texel operations of a BC format are available. The BC lane lands the
///     <see cref="TexelFormatRegistry" /> arms independently of the container lanes, so container tests skip only
///     while an arm is missing instead of passing silently.
/// </summary>
internal static class BcTexelOperationsGate
{
    /// <summary>
    ///     Gets a value indicating whether the texel operations of the given format are registered.
    /// </summary>
    /// <param name="texelFormat">The texel format.</param>
    /// <returns><see langword="true" /> when the operations are registered; otherwise, <see langword="false" />.</returns>
    internal static bool IsAvailable(TexelFormat texelFormat)
    {
        try
        {
            _ = TexelFormatRegistry.GetOperations(texelFormat);
            return true;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>
    ///     Gets the discovery-time skip reason for a format whose operations are not registered yet.
    /// </summary>
    /// <param name="texelFormat">The texel format.</param>
    /// <returns>The skip reason.</returns>
    internal static string GetSkipReason(TexelFormat texelFormat)
        => $"BC texel operations for '{texelFormat}' are not registered yet; the TexelFormatRegistry arm is pending.";
}

/// <summary>
///     A fact that is skipped while the texel operations of the given BC format are not registered.
/// </summary>
internal sealed class BcFactAttribute : FactAttribute
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="BcFactAttribute" /> class.
    /// </summary>
    /// <param name="texelFormat">The BC texel format the test requires.</param>
    public BcFactAttribute(TexelFormat texelFormat)
    {
        if (!BcTexelOperationsGate.IsAvailable(texelFormat))
        {
            this.Skip = BcTexelOperationsGate.GetSkipReason(texelFormat);
        }
    }
}

/// <summary>
///     A theory that is skipped while the texel operations of the given BC format are not registered.
/// </summary>
internal sealed class BcTheoryAttribute : TheoryAttribute
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="BcTheoryAttribute" /> class.
    /// </summary>
    /// <param name="texelFormat">The BC texel format the test requires.</param>
    public BcTheoryAttribute(TexelFormat texelFormat)
    {
        if (!BcTexelOperationsGate.IsAvailable(texelFormat))
        {
            this.Skip = BcTexelOperationsGate.GetSkipReason(texelFormat);
        }
    }
}
