namespace ImageSharp.Textures;

/// <summary>
///     The exception that is thrown when a texture container format cannot be determined.
/// </summary>
public sealed class UnknownTextureFormatException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="UnknownTextureFormatException" /> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public UnknownTextureFormatException(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="UnknownTextureFormatException" /> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public UnknownTextureFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
